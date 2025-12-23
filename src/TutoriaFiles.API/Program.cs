using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Text;
using TutoriaFiles.Infrastructure;
using TutoriaFiles.API.Authentication;
using AspNetCoreRateLimit;

// ============================================================================
// STARTUP DIAGNOSTICS - Early logging before anything else
// ============================================================================
var startupStopwatch = Stopwatch.StartNew();
var startupTimestamp = DateTime.UtcNow;

Console.WriteLine("=============================================================");
Console.WriteLine("TutoriaFiles API - Startup Diagnostics");
Console.WriteLine("=============================================================");
Console.WriteLine($"[STARTUP] Timestamp (UTC): {startupTimestamp:yyyy-MM-dd HH:mm:ss.fff}");
Console.WriteLine($"[STARTUP] Process ID: {Environment.ProcessId}");
Console.WriteLine($"[STARTUP] Machine Name: {Environment.MachineName}");
Console.WriteLine($"[STARTUP] OS: {Environment.OSVersion}");
Console.WriteLine($"[STARTUP] .NET Version: {Environment.Version}");
Console.WriteLine($"[STARTUP] 64-bit Process: {Environment.Is64BitProcess}");
Console.WriteLine($"[STARTUP] Working Directory: {Environment.CurrentDirectory}");
Console.WriteLine($"[STARTUP] Command Line: {Environment.CommandLine}");
Console.WriteLine("-------------------------------------------------------------");

// Log Azure-specific environment variables (helps diagnose App Service issues)
Console.WriteLine("[STARTUP] Azure Environment Variables:");
var azureEnvVars = new[]
{
    "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "WEBSITE_SITE_NAME",
    "WEBSITE_INSTANCE_ID", "WEBSITE_SKU", "WEBSITE_HOSTNAME",
    "PORT", "WEBSITES_PORT", "HTTP_PLATFORM_PORT",
    "ASPNETCORE_URLS", "DOTNET_RUNNING_IN_CONTAINER"
};
foreach (var envVar in azureEnvVars)
{
    var value = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrEmpty(value))
        Console.WriteLine($"  {envVar}={value}");
}
Console.WriteLine("-------------------------------------------------------------");

try
{
    Console.WriteLine("[PHASE 1/8] Creating WebApplicationBuilder...");
    var phaseStopwatch = Stopwatch.StartNew();

    var builder = WebApplication.CreateBuilder(args);

    // Configure Kestrel to allow large file uploads (15MB)
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Limits.MaxRequestBodySize = 15728640; // 15 MB in bytes (15 * 1024 * 1024)
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5); // 5 minutes for slow connections
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5); // 5 minutes keep-alive
    });

    Console.WriteLine($"[PHASE 1/8] WebApplicationBuilder created ({phaseStopwatch.ElapsedMilliseconds}ms)");
    Console.WriteLine($"  Environment: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"  ContentRootPath: {builder.Environment.ContentRootPath}");

    // ============================================================================
    // PHASE 2: Configure Logging
    // ============================================================================
    Console.WriteLine("[PHASE 2/8] Configuring logging...");
    phaseStopwatch.Restart();

    // Configure built-in logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Logging.AddFilter("System", LogLevel.Warning);

    Console.WriteLine($"[PHASE 2/8] Logging configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 3: Configure Rate Limiting & Form Options
    // ============================================================================
    Console.WriteLine("[PHASE 3/8] Configuring rate limiting and form options...");
    phaseStopwatch.Restart();

    // Add Rate Limiting
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // Configure form options to allow large file uploads
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 15728640; // 15 MB
        options.ValueLengthLimit = 15728640;
        options.MultipartHeadersLengthLimit = 15728640;
    });

    // Add services to the container
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Use camelCase for JSON property names
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    builder.Services.AddEndpointsApiExplorer();

    // Add HttpClient for calling TutoriaApi
    builder.Services.AddHttpClient();

    Console.WriteLine($"[PHASE 3/8] Rate limiting and form options configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 4: Configure Authentication
    // ============================================================================
    Console.WriteLine("[PHASE 4/8] Configuring authentication...");
    phaseStopwatch.Restart();

    // Configure JWT Authentication with TutoriaApi validation + local fallback
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"];
    var tutoriaApiUrl = builder.Configuration["TutoriaApi:BaseUrl"];

    Console.WriteLine($"  JWT SecretKey configured: {!string.IsNullOrWhiteSpace(secretKey)}");
    Console.WriteLine($"  TutoriaApi BaseUrl configured: {!string.IsNullOrWhiteSpace(tutoriaApiUrl)}");
    if (!string.IsNullOrWhiteSpace(tutoriaApiUrl))
        Console.WriteLine($"  TutoriaApi BaseUrl: {tutoriaApiUrl}");

    if (!string.IsNullOrWhiteSpace(secretKey) || !string.IsNullOrWhiteSpace(tutoriaApiUrl))
    {
        // Register custom authentication handler that calls TutoriaApi first, then falls back to local validation
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<JwtBearerOptions, TutoriaAuthenticationHandler>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    // These options are used by the custom handler for local fallback
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = string.IsNullOrWhiteSpace(secretKey)
                            ? null
                            : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                        ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings["Issuer"]),
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings["Audience"]),
                        ValidAudience = jwtSettings["Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });

        builder.Services.AddAuthorization(options =>
        {
            // ProfessorOrAbove: professor, super_admin
            options.AddPolicy("ProfessorOrAbove", policy =>
                policy.RequireRole("professor", "super_admin"));

            // AdminOrAbove: admin professor, super_admin
            options.AddPolicy("AdminOrAbove", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "super_admin") ||
                    (context.User.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "professor") &&
                     context.User.HasClaim(c => c.Type == "isAdmin" && c.Value == "True"))));

            // SuperAdminOnly: only super_admin
            options.AddPolicy("SuperAdminOnly", policy =>
                policy.RequireRole("super_admin"));
        });

        Console.WriteLine($"  [OK] JWT Authentication configured (mode: {(string.IsNullOrWhiteSpace(tutoriaApiUrl) ? "local only" : "TutoriaApi + local fallback")})");
    }
    else
    {
        // No authentication - all endpoints are open (use with caution!)
        Console.WriteLine("  [WARNING] No JWT authentication configured - API is public!");
    }

    Console.WriteLine($"[PHASE 4/8] Authentication configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 5: Configure Swagger
    // ============================================================================
    Console.WriteLine("[PHASE 5/8] Configuring Swagger...");
    phaseStopwatch.Restart();

    // Configure Swagger
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TutoriaFiles API",
            Version = "v1",
            Description = "Dedicated file upload/download API for Tutoria platform\n\n" +
                          "Handles large file uploads (up to 15MB) to Azure Blob Storage"
        });

        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            // Add JWT Bearer authentication to Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token. Example: 'Bearer eyJhbGci...'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        }
    });

    Console.WriteLine($"[PHASE 5/8] Swagger configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 6: Register Infrastructure Services (Azure Blob, DB, etc.)
    // ============================================================================
    Console.WriteLine("[PHASE 6/8] Registering infrastructure services...");
    phaseStopwatch.Restart();

    // Log Azure Storage configuration (masked)
    var azureStorageConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    var azureStorageContainer = builder.Configuration["AzureStorage:ContainerName"];
    Console.WriteLine($"  AzureStorage:ConnectionString configured: {!string.IsNullOrWhiteSpace(azureStorageConnectionString)}");
    if (!string.IsNullOrWhiteSpace(azureStorageConnectionString))
    {
        // Extract account name from connection string for logging (safe to log)
        var accountNameMatch = System.Text.RegularExpressions.Regex.Match(
            azureStorageConnectionString, @"AccountName=([^;]+)");
        if (accountNameMatch.Success)
            Console.WriteLine($"  AzureStorage:AccountName: {accountNameMatch.Groups[1].Value}");
    }
    Console.WriteLine($"  AzureStorage:ContainerName: {azureStorageContainer ?? "(not set)"}");

    // Log database connection (masked)
    var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"  Database ConnectionString configured: {!string.IsNullOrWhiteSpace(dbConnectionString)}");
    if (!string.IsNullOrWhiteSpace(dbConnectionString))
    {
        // Extract server name from connection string for logging (safe to log)
        var serverMatch = System.Text.RegularExpressions.Regex.Match(
            dbConnectionString, @"Server=([^;]+)");
        if (serverMatch.Success)
            Console.WriteLine($"  Database Server: {serverMatch.Groups[1].Value}");
    }

    // Add Infrastructure services (Blob Storage, Services) - automatically registered!
    builder.Services.AddInfrastructure(builder.Configuration);

    Console.WriteLine($"[PHASE 6/8] Infrastructure services registered ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 7: Configure CORS
    // ============================================================================
    Console.WriteLine("[PHASE 7/8] Configuring CORS...");
    phaseStopwatch.Restart();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                    "https://app.tutoria.tec.br",           // Production frontend
                    "https://app.dev.tutoria.tec.br",       // Dev frontend
                    "https://tutoria-ui.vercel.app",        // Vercel deployment
                    "http://localhost:3000",                // Local development
                    "https://localhost:3000"                // Local development HTTPS
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    Console.WriteLine($"[PHASE 7/8] CORS configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 8: Build Application
    // ============================================================================
    Console.WriteLine("[PHASE 8/8] Building application...");
    phaseStopwatch.Restart();

    var app = builder.Build();

    Console.WriteLine($"[PHASE 8/8] Application built ({phaseStopwatch.ElapsedMilliseconds}ms)");
    Console.WriteLine("-------------------------------------------------------------");
    Console.WriteLine($"[STARTUP] All phases completed in {startupStopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine("-------------------------------------------------------------");

    // ============================================================================
    // Configure HTTP Request Pipeline
    // ============================================================================
    Console.WriteLine("[PIPELINE] Configuring HTTP request pipeline...");

    // Swagger enabled in all environments
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TutoriaFiles API v1");
    });

    // Disable HTTPS redirection in development (breaks CORS preflight)
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();
    app.UseIpRateLimiting();

    if (!string.IsNullOrWhiteSpace(secretKey))
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.MapControllers();

    // Health check endpoints
    app.MapGet("/ping", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

    Console.WriteLine("[PIPELINE] HTTP request pipeline configured");

    // ============================================================================
    // Final Startup Summary
    // ============================================================================
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    Console.WriteLine("=============================================================");
    Console.WriteLine("TutoriaFiles API - Ready to Start");
    Console.WriteLine("=============================================================");
    Console.WriteLine($"[READY] Total startup time: {startupStopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"[READY] Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"[READY] Endpoints:");
    Console.WriteLine("  - GET  /ping (health check)");
    Console.WriteLine("  - POST /api/files/upload (max 15MB)");
    Console.WriteLine("  - GET  /api/files/download");
    Console.WriteLine("  - DELETE /api/files/delete");
    Console.WriteLine("  - GET  /swagger (API documentation)");
    Console.WriteLine("=============================================================");
    Console.WriteLine("[READY] Starting Kestrel web server...");

    logger.LogInformation("TutoriaFiles API started successfully in {ElapsedMs}ms", startupStopwatch.ElapsedMilliseconds);

    app.Run();
}
catch (Exception ex)
{
    // ============================================================================
    // STARTUP FAILURE - Log everything possible
    // ============================================================================
    Console.WriteLine("=============================================================");
    Console.WriteLine("FATAL: TutoriaFiles API failed to start!");
    Console.WriteLine("=============================================================");
    Console.WriteLine($"[FATAL] Exception Type: {ex.GetType().FullName}");
    Console.WriteLine($"[FATAL] Message: {ex.Message}");
    Console.WriteLine($"[FATAL] Time elapsed before crash: {startupStopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine("-------------------------------------------------------------");
    Console.WriteLine("[FATAL] Stack Trace:");
    Console.WriteLine(ex.StackTrace);

    if (ex.InnerException != null)
    {
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine("[FATAL] Inner Exception:");
        Console.WriteLine($"  Type: {ex.InnerException.GetType().FullName}");
        Console.WriteLine($"  Message: {ex.InnerException.Message}");
        Console.WriteLine($"  Stack Trace: {ex.InnerException.StackTrace}");
    }

    Console.WriteLine("=============================================================");
    Console.WriteLine("[FATAL] Possible causes to investigate:");
    Console.WriteLine("  1. Missing or invalid Azure Storage connection string");
    Console.WriteLine("  2. Missing or invalid database connection string");
    Console.WriteLine("  3. Port binding issues (check ASPNETCORE_URLS or PORT env vars)");
    Console.WriteLine("  4. Missing configuration in Azure App Service Application Settings");
    Console.WriteLine("  5. Assembly loading issues (check all dependencies are published)");
    Console.WriteLine("=============================================================");

    // Re-throw to ensure Azure App Service sees the failure
    throw;
}
