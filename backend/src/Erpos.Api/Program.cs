using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using Erpos.Api.Infrastructure;
using Erpos.Application;
using Erpos.Application.Common;
using Erpos.Infrastructure;
using Erpos.Infrastructure.Persistence;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Production logs are one JSON object per line (for Loki/ELK/CloudWatch); development keeps the readable console.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ"; o.UseUtcTimestamp = true; });
}

// Behind nginx/a load balancer: trust X-Forwarded-For/Proto so client IPs (rate limiting, logs) and HTTPS are right.
// The API port must only be reachable through the proxy (as in docker-compose.prod.yml).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers(o => o.Filters.Add<TransactionFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod()));

// Login and token refresh: per client IP, a fixed number of attempts per minute (account lockout handles slow guessing).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = builder.Configuration.GetValue("Security:AuthRequestsPerMinute", 100),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ERPOS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

var app = builder.Build();

// Migrations and data upgrades run once per deployment: `dotnet Erpos.Api.dll --migrate` (a one-off job before rollout),
// or on startup when Database:MigrateOnStartup is true (the default in Development only). Several API instances
// starting together would otherwise race to migrate the same database.
var migrateOnly = args.Contains("--migrate");
using (var scope = app.Services.CreateScope())
{
    if (migrateOnly || app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().RunAsync();
    else
    {
        var pending = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
            throw new InvalidOperationException($"The database is missing {pending.Count} migration(s) ({pending[^1]}). Run the API once with --migrate first.");
    }
}
if (migrateOnly)
{
    app.Logger.LogInformation("Migrations and data upgrades applied.");
    return;
}

app.UseForwardedHeaders();

app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Every log line written while handling a request carries the organization and user.
app.Use(async (ctx, next) =>
{
    var user = ctx.RequestServices.GetRequiredService<ICurrentUser>();
    using (app.Logger.BeginScope(new Dictionary<string, object?> { ["TenantId"] = user.TenantId, ["UserId"] = user.UserId }))
        await next();
});
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
// Liveness: the process answers. Readiness: it can also reach the database (use for load balancer / orchestrator checks).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();
