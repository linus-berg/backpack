using System.Configuration;
using System.Security.Claims;
using System.Text.Json;
using Collector.Kernel;
using Core.Infrastructure;
using Core.Infrastructure.Health;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Health;
using Core.Kernel.Registrations;
using Core.Services;
using Integration.API;
using Integration.API.Errors;
using Integration.API.Health;
using Integration.API.Jobs;
using Integration.API.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using Configuration = Core.Kernel.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddTelemetry(
  new ModuleRegistration(ModuleType.CORE, typeof(IHost))
);

builder.Host.UseSerilog(
  (context, configuration) => {
    configuration.Enrich.FromLogContext();
    configuration.MinimumLevel.Override("Microsoft", LogEventLevel.Information);
    configuration.WriteTo.Console();
    configuration.WriteTo.File(
      Path.Combine(
        Environment.GetEnvironmentVariable("BP_LOGS") ?? "logs/",
        "backpack_api.log"
      )
    );
  }
);

// Add services to the container.
builder.Services.AddMassTransit(
  b => {
    b.UsingRabbitMq(
      (ctx, cfg) => {
        cfg.Host(
          Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_HOST),
          "/",
          h => {
            h.Username(
              Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER)
            );
            h.Password(
              Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS)
            );
          }
        );
        cfg.ConfigureEndpoints(ctx);
      }
    );
  }
);

builder.Services.AddSingleton<IConnectionMultiplexer>(
  ConnectionMultiplexer.Connect(
    new ConfigurationOptions {
      User = Configuration.GetBackpackVariable(CoreVariables.BP_REDIS_USER),
      Password = Configuration.GetBackpackVariable(CoreVariables.BP_REDIS_PASS),
      EndPoints = new EndPointCollection {
        Configuration.GetBackpackVariable(CoreVariables.BP_REDIS_HOST)
      }
    }
  )
);

builder.Services.AddSingleton<PreviewRoutingService>();
builder.Services.AddScoped<ICoreDatabase, MongoDatabase>();
builder.Services.AddSingleton<ICoreCache, CoreCache>();
builder.Services.AddScoped<IArtifactService, ArtifactService>();
builder.Services.AddSingleton<IStatusService, RabbitMqStatusService>();
builder.Services.AddScoped<IEventService, EventService>();
/* Background jobs (re-track and validate), one at a time across replicas. */
builder.Services.AddSingleton<IJobService, MongoJobService>();
builder.Services.AddScoped<JobRunner>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddScoped<JobStarter>();
builder.Services.AddHostedService<JobWorker>();

/* Authentication & Authorization */
string? authority =
  Configuration.GetBackpackVariable(CoreVariables.BP_OIDC_AUTHORITY);
string? audience =
  Configuration.GetBackpackVariable(CoreVariables.BP_OIDC_AUDIENCE);

if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(audience)) {
  throw new ConfigurationErrorsException("OIDC is not configured.");
}

builder.Services.AddAuthentication(
         options => {
           options.DefaultAuthenticateScheme =
             JwtBearerDefaults.AuthenticationScheme;
           options.DefaultChallengeScheme =
             JwtBearerDefaults.AuthenticationScheme;
         }
       )
       .AddJwtBearer(
         options => {
           options.Authority = authority;
           options.Audience = audience;
           options.RequireHttpsMetadata = false; // Set to true in production

           options.TokenValidationParameters = new TokenValidationParameters {
             ValidateIssuer = true,
             ValidIssuers = new[] {
               authority,
               authority.TrimEnd('/') + "/",
               authority.TrimEnd('/')
             },
             ValidateAudience = false,
             ValidateLifetime = true,
             NameClaimType = "preferred_username",
             RoleClaimType = ClaimTypes.Role
           };

           options.Events = new JwtBearerEvents {
             OnTokenValidated = context => {
               if (context.Principal?.Identity is ClaimsIdentity identity) {
                 // Map Keycloak/Standard OIDC resource roles to .NET roles
                 Claim? resource_access_claim =
                   identity.FindFirst("resource_access");
                 if (resource_access_claim != null) {
                   try {
                     using JsonDocument json_doc =
                       JsonDocument.Parse(resource_access_claim.Value);
                     if (json_doc.RootElement.TryGetProperty(
                           audience,
                           out JsonElement client_element
                         ) &&
                         client_element.TryGetProperty(
                           "roles",
                           out JsonElement roles_element
                         )) {
                       foreach (JsonElement role in
                                roles_element.EnumerateArray()) {
                         identity.AddClaim(
                           new Claim(ClaimTypes.Role, role.GetString()!)
                         );
                       }
                     }
                   } catch {
                     // Log or handle parsing error
                   }
                 }

                 // Also handle a flat 'roles' claim if present
                 IEnumerable<Claim> roles_claim = identity.FindAll("roles");
                 foreach (Claim rc in roles_claim) {
                   identity.AddClaim(new Claim(ClaimTypes.Role, rc.Value));
                 }
               }

               return Task.CompletedTask;
             }
           };
         }
       )
       .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
         ApiKeyAuthenticationOptions.C_SCHEME,
         null
       );

builder.Services.AddAuthorization(
  options => {
    AuthorizationPolicy default_policy = new AuthorizationPolicyBuilder(
                                           JwtBearerDefaults
                                             .AuthenticationScheme,
                                           ApiKeyAuthenticationOptions.C_SCHEME
                                         )
                                         .RequireAuthenticatedUser()
                                         .Build();
    options.DefaultPolicy = default_policy;
  }
);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
/* Unhandled exceptions become RFC 7807 ProblemDetails (status, title,
   traceId) in every environment. Without this, Development returns the
   developer exception page, whose stack trace and request headers
   (including the bearer token) the GUI would show to the user. */
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<InvalidCollectLocationExceptionHandler>();

/* /healthz/live and /healthz/ready for Kubernetes probes, and the
   infrastructure checks of the Status page (/api/status/health). MassTransit
   adds its bus check. RabbitMQ, the management API, S3 and the OIDC provider
   only degrade readiness, since the API still serves reads and API keys
   without them. */
builder.Services.AddHttpClient("health", c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<InfrastructureHealthService>();
builder.Services.AddHealthChecks()
       .AddMongo()
       .AddRedis()
       .AddCheck<RabbitMqHealthCheck>(
         "rabbitmq",
         HealthStatus.Degraded,
         HealthTags.READY_INFRASTRUCTURE,
         TimeSpan.FromSeconds(3)
       )
       .AddCheck<RabbitMqManagementHealthCheck>(
         "rabbitmq-management",
         HealthStatus.Degraded,
         HealthTags.READY_INFRASTRUCTURE,
         TimeSpan.FromSeconds(3)
       )
       .AddCheck<OidcHealthCheck>(
         "oidc",
         HealthStatus.Degraded,
         HealthTags.READY_INFRASTRUCTURE,
         TimeSpan.FromSeconds(3)
       );
/* S3 is optional for the API: it only reports it, given the collectors'
   S3 settings. Without them the Status page shows S3 as not checked. */
if (!string.IsNullOrWhiteSpace(
      Environment.GetEnvironmentVariable(nameof(CoreVariables.BP_S3_ENDPOINT))
    ) &&
    new[] {
      CoreVariables.BP_S3_REGION, CoreVariables.BP_S3_BUCKET,
      CoreVariables.BP_S3_ACCESS_KEY, CoreVariables.BP_S3_SECRET_KEY
    }.All(v => Environment.GetEnvironmentVariable(v.ToString()) != null)) {
  builder.Services.AddS3Storage(HealthStatus.Degraded, false);
}
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseCors(
  b => {
    b.AllowAnyOrigin();
    b.AllowAnyHeader();
    b.AllowAnyMethod();
  }
);
/* After UseCors, so error responses still carry CORS headers and the GUI
   can read them. Exceptions are logged by this middleware. */
app.UseExceptionHandler();
/* Probes hit /healthz every few seconds; keep them out of the request log. */
app.UseSerilogRequestLogging(
  options => options.GetLevel = (context, _, error) =>
               context.Request.Path.StartsWithSegments("/healthz")
                 ? LogEventLevel.Verbose
                 : error != null || context.Response.StatusCode >= 500
                   ? LogEventLevel.Error
                   : LogEventLevel.Information
);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(
     "/healthz/live",
     new HealthCheckOptions {
       Predicate = _ => false,
       ResponseWriter = WriteHealth
     }
   )
   .AllowAnonymous();
app.MapHealthChecks(
     "/healthz/ready",
     new HealthCheckOptions {
       Predicate = check => check.Tags.Contains(HealthTags.READY),
       ResponseWriter = WriteHealth
     }
   )
   .AllowAnonymous();
app.Run();

/* The same JSON as the worker services' health endpoint. */
static Task WriteHealth(HttpContext context, HealthReport report) {
  context.Response.ContentType = "application/json";
  return context.Response.WriteAsync(HealthReportJson.Serialize(report));
}