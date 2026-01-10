using docker_compose_manager_back.Data;
using docker_compose_manager_back.Filters;
using docker_compose_manager_back.Hubs;
using docker_compose_manager_back.Middleware;
using docker_compose_manager_back.Services;
using docker_compose_manager_back.Validators;
using DockerComposeManager.Services.Security;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure Serilog with explicit assemblies (single-file publish safe)
try
{
    Serilog.Settings.Configuration.ConfigurationReaderOptions readerOptions = new(
        typeof(Serilog.Sinks.SystemConsole.Themes.ConsoleTheme).Assembly,
        typeof(Serilog.Sinks.File.FileSink).Assembly
    );

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration, readerOptions)
        .CreateLogger();
}
catch (Exception ex)
{
    // Fallback minimal logger if configuration loading fails
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();
    Log.Error(ex, "Failed to initialize Serilog from configuration; fallback console logger in use");
}

builder.Host.UseSerilog();

// Ensure log directories exist for any configured file sinks
try
{
    IConfigurationSection writeToSection = builder.Configuration.GetSection("Serilog:WriteTo");
    foreach (IConfigurationSection sink in writeToSection.GetChildren())
    {
        string? path = sink.GetSection("Args").GetValue<string>("path");
        if (!string.IsNullOrWhiteSpace(path))
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log.Information("Created log directory: {LogDirectory}", directory);
            }
        }
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to ensure log directories for Serilog file sinks");
}

// Add Database Context
string connectionString = builder.Configuration["Database:ConnectionString"] ?? "Data Source=Data/app.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure JWT Authentication
string jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "JWT Secret is not configured. Please set the JWT_SECRET environment variable or Jwt:Secret in appsettings.json. " +
        "The secret must be at least 32 characters long for security.");
}
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT Secret must be at least 32 characters long. Current length: {jwtSecret.Length}. " +
        "Please set a secure JWT_SECRET environment variable.");
}
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

// Configure password hashing
builder.Services.Configure<PasswordHashingOptions>(
    builder.Configuration.GetSection(PasswordHashingOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

// Add Memory Cache (required for ComposeDiscoveryService)
builder.Services.AddMemoryCache();

// Configure Compose Discovery Options
builder.Services.Configure<docker_compose_manager_back.Configuration.ComposeDiscoveryOptions>(
    builder.Configuration.GetSection("ComposeDiscovery"));

// Register application services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ComposeService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<OperationService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<DockerService>();

// Register Docker Compose services (new architecture)
builder.Services.AddSingleton<DockerCommandExecutor>();
builder.Services.AddScoped<IComposeDiscoveryService, ComposeDiscoveryService>();
builder.Services.AddScoped<IComposeOperationService, ComposeOperationService>();

// Register background services
// DEPRECATED: File discovery service replaced by Docker-only discovery
// builder.Services.AddHostedService<docker_compose_manager_back.BackgroundServices.ComposeFileDiscoveryService>();
builder.Services.AddHostedService<DockerEventsMonitorService>();

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

// Configure Serilog request logging to exclude stats endpoints (to prevent log flooding)
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        // Don't log stats endpoints to prevent flooding
        if (httpContext.Request.Path.StartsWithSegments("/api/containers") &&
            httpContext.Request.Path.Value?.Contains("/stats") == true)
        {
            return Serilog.Events.LogEventLevel.Verbose; // Changed to Verbose (won't show unless explicitly configured)
        }

        // Log errors as Error level
        if (ex != null || httpContext.Response.StatusCode > 499)
        {
            return Serilog.Events.LogEventLevel.Error;
        }

        // Normal requests as Information
        return Serilog.Events.LogEventLevel.Information;
    };
});

app.UseCors();

// Add Security Headers
app.UseSecurityHeaders();

// Add Rate Limiting
//app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Basic health check endpoint (for Docker healthcheck) - just checks if app is running
app.MapGet("/health", () =>
{
    return Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
})
   .WithName("HealthCheck")
   .WithTags("Health")
   .AllowAnonymous();

// Detailed health check endpoint with DB and Docker verification
app.MapGet("/health/detailed", async (AppDbContext dbContext, DockerService dockerService) =>
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
   .WithName("HealthCheckDetailed")
   .WithTags("Health")
   .AllowAnonymous();

app.MapControllers();

// Map SignalR Hubs
app.MapHub<LogsHub>("/hubs/logs");
app.MapHub<OperationsHub>("/hubs/operations");

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
