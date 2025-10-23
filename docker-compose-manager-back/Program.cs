using docker_compose_manager_back.Data;
using docker_compose_manager_back.Middleware;
using docker_compose_manager_back.Hubs;
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
builder.Services.AddSingleton<docker_compose_manager_back.Services.DockerService>();

// Register background services
builder.Services.AddHostedService<docker_compose_manager_back.BackgroundServices.ComposeFileDiscoveryService>();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add Rate Limiting
builder.Services.ConfigureRateLimiting();

// Add SignalR
builder.Services.AddSignalR();

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

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

// Add Rate Limiting
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

app.MapControllers();

// Map SignalR Hub
app.MapHub<LogsHub>("/hubs/logs");

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
