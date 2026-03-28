using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Core.Services;
using Integration.API;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Authorization;
using Keycloak.AuthServices.Common;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

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
            h.Username(Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER));
            h.Password(Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS));
          }
        );
        cfg.ConfigureEndpoints(ctx);
      }
    );
  }
);

builder.Services.AddSingleton<IConnectionMultiplexer>(
  ConnectionMultiplexer.Connect(
    new ConfigurationOptions() {
      User = Configuration.GetBackpackVariable(CoreVariables.BP_REDIS_USER),
      Password = Configuration.GetBackpackVariable(CoreVariables.BP_REDIS_PASS),
      EndPoints = new EndPointCollection() {
        Configuration.GetBackpackVariable(CoreVariables.BP_REDIS_HOST),
      }
    }
  )
);
builder.Services.AddScoped<ICoreDatabase, MongoDatabase>();
builder.Services.AddSingleton<ICoreCache, CoreCache>();
builder.Services.AddScoped<IArtifactService, ArtifactService>();
builder.Services.AddSingleton<IStatusService, RabbitMqStatusService>();
builder.Services.AddScoped<IEventService, EventService>();

/* keycloak */
builder.Host.ConfigureKeycloakConfigurationSource();

builder.Services.AddKeycloakAuthentication(
  builder.Configuration,
  o => { o.RequireHttpsMetadata = false; }
);

builder.Services.AddAuthentication()
  .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationOptions.C_SCHEME, null);
builder.Services.AddAuthorization(options => {
  var defaultPolicy = new AuthorizationPolicyBuilder(
      JwtBearerDefaults.AuthenticationScheme,
      ApiKeyAuthenticationOptions.C_SCHEME)
    .RequireAuthenticatedUser()
    .Build();
  options.DefaultPolicy = defaultPolicy;
});

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
