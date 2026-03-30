using System.Configuration;
using System.Security.Claims;
using System.Text.Json;
using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Core.Services;
using Integration.API;
using Integration.API.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
        Environment.GetEnvironmentVariable("BP_LOGS"),
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
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();