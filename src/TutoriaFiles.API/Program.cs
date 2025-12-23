using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using TutoriaFiles.Infrastructure;
using TutoriaFiles.API.Authentication;
using AspNetCoreRateLimit;

// ============================================================================
// FAILSAFE FILE LOGGING - Writes to disk even if console/stdout fails
// ============================================================================
var startupLogPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
void LogStartup(string message)
{
    var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
    Console.WriteLine(line);
    try
    {
        File.AppendAllText(startupLogPath, line + Environment.NewLine);
    }
    catch { /* Ignore file write errors */ }
}

LogStartup("========== TUTORIAFILES STARTUP BEGIN ==========");
LogStartup($"Startup log location: {startupLogPath}");

// ============================================================================
// STARTUP DIAGNOSTICS - Early logging before anything else
// ============================================================================
var startupStopwatch = Stopwatch.StartNew();
var startupTimestamp = DateTime.UtcNow;

LogStartup("=============================================================");
LogStartup("TutoriaFiles API - Startup Diagnostics");
LogStartup("=============================================================");
LogStartup($"[STARTUP] Timestamp (UTC): {startupTimestamp:yyyy-MM-dd HH:mm:ss.fff}");
LogStartup($"[STARTUP] Process ID: {Environment.ProcessId}");
LogStartup($"[STARTUP] Machine Name: {Environment.MachineName}");
LogStartup($"[STARTUP] OS: {Environment.OSVersion}");
LogStartup($"[STARTUP] .NET Version: {Environment.Version}");
LogStartup($"[STARTUP] 64-bit Process: {Environment.Is64BitProcess}");
LogStartup($"[STARTUP] Base Directory: {AppContext.BaseDirectory}");
LogStartup($"[STARTUP] Working Directory: {Environment.CurrentDirectory}");
LogStartup($"[STARTUP] Command Line: {Environment.CommandLine}");
LogStartup("-------------------------------------------------------------");

// Log Azure-specific environment variables (helps diagnose App Service issues)
LogStartup("[STARTUP] Azure Environment Variables:");
var azureEnvVars = new[]
{
    "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "WEBSITE_SITE_NAME",
    "WEBSITE_INSTANCE_ID", "WEBSITE_SKU", "WEBSITE_HOSTNAME",
    "PORT", "WEBSITES_PORT", "HTTP_PLATFORM_PORT",
    "ASPNETCORE_URLS", "DOTNET_RUNNING_IN_CONTAINER", "HOME"
};
foreach (var envVar in azureEnvVars)
{
    var value = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrEmpty(value))
        LogStartup($"  {envVar}={value}");
}
LogStartup("-------------------------------------------------------------");

try
{
    LogStartup("[PHASE 1/8] Creating WebApplicationBuilder...");
    var phaseStopwatch = Stopwatch.StartNew();

    var builder = WebApplication.CreateBuilder(args);

    // Log configuration sources for debugging
    LogStartup("  Configuration sources loaded:");
    foreach (var source in builder.Configuration.Sources)
    {
        LogStartup($"    - {source.GetType().Name}");
    }

    // Log critical config values to verify they're being overridden
    LogStartup("  Configuration values (checking env var override):");
    LogStartup($"    AzureStorage:ConnectionString set: {!string.IsNullOrWhiteSpace(builder.Configuration["AzureStorage:ConnectionString"])}");
    LogStartup($"    ConnectionStrings:DefaultConnection set: {!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection"))}");
    LogStartup($"    TutoriaApi:BaseUrl: {builder.Configuration["TutoriaApi:BaseUrl"] ?? "(not set)"}");

    // Check for common Azure env var naming issues
    LogStartup("  Checking for Azure App Service env vars (use __ for nested):");
    var envVarsToCheck = new[] {
        "AzureStorage__ConnectionString", "AzureStorage__ContainerName",
        "ConnectionStrings__DefaultConnection", "TutoriaApi__BaseUrl"
    };
    foreach (var envVar in envVarsToCheck)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        LogStartup($"    {envVar}: {(string.IsNullOrEmpty(value) ? "(NOT SET)" : "SET")}");
    }

    // Configure Kestrel to allow large file uploads (15MB)
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Limits.MaxRequestBodySize = 15728640; // 15 MB in bytes (15 * 1024 * 1024)
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5); // 5 minutes for slow connections
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5); // 5 minutes keep-alive
    });

    LogStartup($"[PHASE 1/8] WebApplicationBuilder created ({phaseStopwatch.ElapsedMilliseconds}ms)");
    LogStartup($"  Environment: {builder.Environment.EnvironmentName}");
    LogStartup($"  ContentRootPath: {builder.Environment.ContentRootPath}");

    // ============================================================================
    // PHASE 2: Configure Logging
    // ============================================================================
    LogStartup("[PHASE 2/8] Configuring logging...");
    phaseStopwatch.Restart();

    // Configure built-in logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Logging.AddFilter("System", LogLevel.Warning);

    LogStartup($"[PHASE 2/8] Logging configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 3: Configure Rate Limiting & Form Options
    // ============================================================================
    LogStartup("[PHASE 3/8] Configuring rate limiting and form options...");
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

    LogStartup($"[PHASE 3/8] Rate limiting and form options configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 4: Configure Authentication (validates tokens via TutoriaApi)
    // ============================================================================
    LogStartup("[PHASE 4/8] Configuring authentication...");
    phaseStopwatch.Restart();

    var tutoriaApiUrl = builder.Configuration["TutoriaApi:BaseUrl"];

    if (string.IsNullOrWhiteSpace(tutoriaApiUrl))
    {
        throw new InvalidOperationException("TutoriaApi:BaseUrl must be configured - tokens are validated against this API");
    }

    LogStartup($"  TutoriaApi BaseUrl: {tutoriaApiUrl}");

    // Register custom authentication handler that validates tokens via TutoriaApi
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddScheme<JwtBearerOptions, TutoriaAuthenticationHandler>(
            JwtBearerDefaults.AuthenticationScheme,
            options => { }); // No local options needed - we validate via TutoriaApi

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

    LogStartup("  [OK] Authentication configured (tokens validated via TutoriaApi)");
    LogStartup($"[PHASE 4/8] Authentication configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 5: Configure Swagger
    // ============================================================================
    LogStartup("[PHASE 5/8] Configuring Swagger...");
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

        // Add JWT Bearer authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Enter 'Bearer' [space] and your token from TutoriaApi. Example: 'Bearer eyJhbGci...'",
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
    });

    LogStartup($"[PHASE 5/8] Swagger configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 6: Register Infrastructure Services (Azure Blob, DB, etc.)
    // ============================================================================
    LogStartup("[PHASE 6/8] Registering infrastructure services...");
    phaseStopwatch.Restart();

    // Log Azure Storage configuration (masked)
    var azureStorageConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    var azureStorageContainer = builder.Configuration["AzureStorage:ContainerName"];
    LogStartup($"  AzureStorage:ConnectionString configured: {!string.IsNullOrWhiteSpace(azureStorageConnectionString)}");
    if (!string.IsNullOrWhiteSpace(azureStorageConnectionString))
    {
        // Extract account name from connection string for logging (safe to log)
        var accountNameMatch = System.Text.RegularExpressions.Regex.Match(
            azureStorageConnectionString, @"AccountName=([^;]+)");
        if (accountNameMatch.Success)
            LogStartup($"  AzureStorage:AccountName: {accountNameMatch.Groups[1].Value}");
    }
    LogStartup($"  AzureStorage:ContainerName: {azureStorageContainer ?? "(not set)"}");

    // Log database connection (masked)
    var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    LogStartup($"  Database ConnectionString configured: {!string.IsNullOrWhiteSpace(dbConnectionString)}");
    if (!string.IsNullOrWhiteSpace(dbConnectionString))
    {
        // Extract server name from connection string for logging (safe to log)
        var serverMatch = System.Text.RegularExpressions.Regex.Match(
            dbConnectionString, @"Server=([^;]+)");
        if (serverMatch.Success)
            LogStartup($"  Database Server: {serverMatch.Groups[1].Value}");
    }

    // Add Infrastructure services (Blob Storage, Services) - automatically registered!
    builder.Services.AddInfrastructure(builder.Configuration);

    LogStartup($"[PHASE 6/8] Infrastructure services registered ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 7: Configure CORS
    // ============================================================================
    LogStartup("[PHASE 7/8] Configuring CORS...");
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

    LogStartup($"[PHASE 7/8] CORS configured ({phaseStopwatch.ElapsedMilliseconds}ms)");

    // ============================================================================
    // PHASE 8: Build Application
    // ============================================================================
    LogStartup("[PHASE 8/8] Building application...");
    phaseStopwatch.Restart();

    var app = builder.Build();

    LogStartup($"[PHASE 8/8] Application built ({phaseStopwatch.ElapsedMilliseconds}ms)");
    LogStartup("-------------------------------------------------------------");
    LogStartup($"[STARTUP] All phases completed in {startupStopwatch.ElapsedMilliseconds}ms");
    LogStartup("-------------------------------------------------------------");

    // ============================================================================
    // Configure HTTP Request Pipeline
    // ============================================================================
    LogStartup("[PIPELINE] Configuring HTTP request pipeline...");

    // FIRST: Add global exception handler to catch ALL errors and log them
    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Log to file so we can see it via Kudu
            var errorLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] REQUEST ERROR: {context.Request.Path}\n" +
                           $"  Exception: {ex.GetType().FullName}\n" +
                           $"  Message: {ex.Message}\n" +
                           $"  Stack: {ex.StackTrace}\n";
            if (ex.InnerException != null)
            {
                errorLog += $"  Inner: {ex.InnerException.Message}\n";
            }
            try { File.AppendAllText(startupLogPath, errorLog); } catch { }

            // Return 500 with error details (remove in production if sensitive)
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"Error: {ex.Message}\n\nSee /debug/startup-log for details");
        }
    });

    // Bare minimum test endpoint - no middleware, no DI
    app.MapGet("/test", () => "OK").ExcludeFromDescription();

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
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoints
    app.MapGet("/ping", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

    // Diagnostic endpoint to view startup log (helps debug Azure issues)
    app.MapGet("/debug/startup-log", () =>
    {
        try
        {
            if (File.Exists(startupLogPath))
            {
                var content = File.ReadAllText(startupLogPath);
                return Results.Text(content, "text/plain");
            }
            return Results.Text($"Startup log not found at: {startupLogPath}", "text/plain");
        }
        catch (Exception ex)
        {
            return Results.Text($"Error reading startup log: {ex.Message}", "text/plain");
        }
    });

    // Diagnostic endpoint to view current config (masks secrets)
    app.MapGet("/debug/config", (IConfiguration config) =>
    {
        var diagnostics = new Dictionary<string, object>
        {
            ["environment"] = app.Environment.EnvironmentName,
            ["azureStorage_connectionString_set"] = !string.IsNullOrWhiteSpace(config["AzureStorage:ConnectionString"]),
            ["azureStorage_containerName"] = config["AzureStorage:ContainerName"] ?? "(not set)",
            ["database_connectionString_set"] = !string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")),
            ["tutoriaApi_baseUrl"] = config["TutoriaApi:BaseUrl"] ?? "(not set)",
            ["startupLogPath"] = startupLogPath,
            ["startupLogExists"] = File.Exists(startupLogPath)
        };
        return Results.Ok(diagnostics);
    });

    LogStartup("[PIPELINE] HTTP request pipeline configured");

    // ============================================================================
    // Final Startup Summary
    // ============================================================================
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    LogStartup("=============================================================");
    LogStartup("TutoriaFiles API - Ready to Start");
    LogStartup("=============================================================");
    LogStartup($"[READY] Total startup time: {startupStopwatch.ElapsedMilliseconds}ms");
    LogStartup($"[READY] Environment: {app.Environment.EnvironmentName}");
    LogStartup($"[READY] Endpoints:");
    LogStartup("  - GET  /ping (health check)");
    LogStartup("  - POST /api/files/upload (max 15MB)");
    LogStartup("  - GET  /api/files/download");
    LogStartup("  - DELETE /api/files/delete");
    LogStartup("  - GET  /swagger (API documentation)");
    LogStartup("=============================================================");
    LogStartup("[READY] Starting Kestrel web server...");

    logger.LogInformation("TutoriaFiles API started successfully in {ElapsedMs}ms", startupStopwatch.ElapsedMilliseconds);

    app.Run();
}
catch (Exception ex)
{
    // ============================================================================
    // STARTUP FAILURE - Log everything possible
    // ============================================================================
    LogStartup("=============================================================");
    LogStartup("FATAL: TutoriaFiles API failed to start!");
    LogStartup("=============================================================");
    LogStartup($"[FATAL] Exception Type: {ex.GetType().FullName}");
    LogStartup($"[FATAL] Message: {ex.Message}");
    LogStartup($"[FATAL] Time elapsed before crash: {startupStopwatch.ElapsedMilliseconds}ms");
    LogStartup("-------------------------------------------------------------");
    LogStartup("[FATAL] Stack Trace:");
    LogStartup(ex.StackTrace ?? "(no stack trace)");

    if (ex.InnerException != null)
    {
        LogStartup("-------------------------------------------------------------");
        LogStartup("[FATAL] Inner Exception:");
        LogStartup($"  Type: {ex.InnerException.GetType().FullName}");
        LogStartup($"  Message: {ex.InnerException.Message}");
        LogStartup($"  Stack Trace: {ex.InnerException.StackTrace ?? "(no stack trace)"}");
    }

    LogStartup("=============================================================");
    LogStartup("[FATAL] Possible causes to investigate:");
    LogStartup("  1. Missing or invalid Azure Storage connection string");
    LogStartup("  2. Missing or invalid database connection string");
    LogStartup("  3. Port binding issues (check ASPNETCORE_URLS or PORT env vars)");
    LogStartup("  4. Missing configuration in Azure App Service Application Settings");
    LogStartup("  5. Assembly loading issues (check all dependencies are published)");
    LogStartup("=============================================================");

    // Re-throw to ensure Azure App Service sees the failure
    throw;
}
