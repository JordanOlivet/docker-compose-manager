using docker_compose_manager_back.Data;
using docker_compose_manager_back.Filters;
using docker_compose_manager_back.Hubs;
using docker_compose_manager_back.Middleware;
using docker_compose_manager_back.Services;
using docker_compose_manager_back.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Database Context
string connectionString = builder.Configuration["Database:ConnectionString"] ?? "Data Source=Data/app.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure JWT Authentication
string jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "docker-compose-manager";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "docker-compose-manager-client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    // Configure JWT authentication for SignalR
    // SignalR sends the token in the query string (access_token parameter)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            Microsoft.Extensions.Primitives.StringValues accessToken = context.Request.Query["access_token"];

            // If the request is for our SignalR hubs
            PathString path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Configure CORS
string[] corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register application services
builder.Services.AddScoped<docker_compose_manager_back.Services.JwtTokenService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.AuthService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.IUserService, docker_compose_manager_back.Services.UserService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.FileService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.ComposeService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.IAuditService, docker_compose_manager_back.Services.AuditService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.OperationService>();
builder.Services.AddScoped<docker_compose_manager_back.Services.IPermissionService, docker_compose_manager_back.Services.PermissionService>();
builder.Services.AddSingleton<docker_compose_manager_back.Services.DockerService>();

// Register background services
builder.Services.AddHostedService<docker_compose_manager_back.BackgroundServices.ComposeFileDiscoveryService>();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add Rate Limiting
//builder.Services.ConfigureRateLimiting();

// Add SignalR
builder.Services.AddSignalR();

// Add controllers with validation filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidateModelStateFilter>();
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

// Configure validation based on environment
ValidationConfig.IsDevelopment = app.Environment.IsDevelopment();
Log.Information("Validation mode: {Mode}", ValidationConfig.IsDevelopment ? "Development (Relaxed)" : "Production (Strict)");

// Apply migrations and seed data
using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Ensure database directory exists
        string dbPath = builder.Configuration["Database:ConnectionString"] ?? "Data Source=Data/app.db";
        string? directory = Path.GetDirectoryName(dbPath.Replace("Data Source=", "").Trim());
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Log.Information($"Created database directory: {directory}");
        }

        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error applying database migrations");
    }
}

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add Error Handling Middleware first
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSerilogRequestLogging();

app.UseCors();

// Add Security Headers
app.UseSecurityHeaders();

// Add Rate Limiting
//app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint with DB and Docker verification
app.MapGet("/health", async (AppDbContext dbContext, DockerService dockerService) =>
{
    Dictionary<string, object> checks = new();
    DateTime startTime = DateTime.UtcNow;
    bool isHealthy = true;

    // Check Database
    try
    {
        await dbContext.Database.CanConnectAsync();
        int userCount = await dbContext.Users.CountAsync();
        checks["database"] = new
        {
            status = "Healthy",
            description = "SQLite database is accessible",
            userCount = userCount
        };
    }
    catch (Exception ex)
    {
        isHealthy = false;
        checks["database"] = new
        {
            status = "Unhealthy",
            description = "Cannot connect to database",
            error = ex.Message
        };
    }

    // Check Docker
    try
    {
        List<docker_compose_manager_back.DTOs.ContainerDto> containers = await dockerService.ListContainersAsync(true);
        checks["docker"] = new
        {
            status = "Healthy",
            description = "Docker daemon is reachable",
            containerCount = containers.Count
        };
    }
    catch (Exception ex)
    {
        isHealthy = false;
        checks["docker"] = new
        {
            status = "Unhealthy",
            description = "Cannot connect to Docker daemon",
            error = ex.Message
        };
    }

    TimeSpan totalDuration = DateTime.UtcNow - startTime;

    var response = new
    {
        status = isHealthy ? "Healthy" : "Unhealthy",
        checks = checks,
        totalDuration = totalDuration.ToString(@"hh\:mm\:ss\.fffffff"),
        timestamp = DateTime.UtcNow
    };

    return isHealthy ? Results.Ok(response) : Results.StatusCode(503);
})
   .WithName("HealthCheck")
   .WithTags("Health")
   .AllowAnonymous();

app.MapControllers();

// Map SignalR Hubs
app.MapHub<LogsHub>("/hubs/logs");
app.MapHub<docker_compose_manager_back.Hubs.OperationsHub>("/hubs/operations");

// Log when application is ready
IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    string[] urls = app.Urls.ToArray();
    if (urls.Length > 0)
    {
        Log.Information("Docker Compose Manager Backend is ready and listening on: {Urls}", string.Join(", ", urls));
    }
    else
    {
        Log.Information("Docker Compose Manager Backend is ready and listening");
    }
});

lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Docker Compose Manager Backend is stopping...");
});

Log.Information("Docker Compose Manager Backend starting...");
app.Run();
Log.Information("Docker Compose Manager Backend stopped");
