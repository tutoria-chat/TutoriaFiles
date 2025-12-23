using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using TutoriaFiles.Infrastructure;
using TutoriaFiles.API.Authentication;
using AspNetCoreRateLimit;

// Simple startup logging
var logPath = Path.Combine(AppContext.BaseDirectory, "app.log");
void Log(string msg)
{
    var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {msg}";
    Console.WriteLine(line);
    try { File.AppendAllText(logPath, line + "\n"); } catch { }
}

Log("=== TutoriaFiles API Starting ===");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Log key config
    var tutoriaApiUrl = builder.Configuration["TutoriaApi:BaseUrl"];
    var hasAzureStorage = !string.IsNullOrWhiteSpace(builder.Configuration["AzureStorage:ConnectionString"]);
    var hasDbConnection = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection"));

    Log($"Environment: {builder.Environment.EnvironmentName}");
    Log($"TutoriaApi:BaseUrl: {tutoriaApiUrl ?? "(NOT SET)"}");
    Log($"AzureStorage configured: {hasAzureStorage}");
    Log($"Database configured: {hasDbConnection}");

    if (string.IsNullOrWhiteSpace(tutoriaApiUrl))
        throw new InvalidOperationException("TutoriaApi:BaseUrl must be configured");

    // Kestrel config for large uploads
    builder.WebHost.ConfigureKestrel(opts =>
    {
        opts.Limits.MaxRequestBodySize = 15728640; // 15MB
        opts.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    });

    // Logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Rate limiting
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // Form options for file uploads
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(opts =>
    {
        opts.MultipartBodyLengthLimit = 15728640;
        opts.ValueLengthLimit = 15728640;
        opts.MultipartHeadersLengthLimit = 15728640;
    });

    // Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpClient();

    // Authentication via TutoriaApi
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddScheme<JwtBearerOptions, TutoriaAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });

    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("ProfessorOrAbove", p => p.RequireRole("professor", "super_admin"));
        opts.AddPolicy("AdminOrAbove", p => p.RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "super_admin") ||
            (ctx.User.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "professor") &&
             ctx.User.HasClaim(c => c.Type == "isAdmin" && c.Value == "True"))));
        opts.AddPolicy("SuperAdminOnly", p => p.RequireRole("super_admin"));
    });

    // Swagger
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo { Title = "TutoriaFiles API", Version = "v1" });
        opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT from TutoriaApi. Example: 'Bearer eyJhbGci...'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });

    // Infrastructure (Azure Blob, DB, services)
    builder.Services.AddInfrastructure(builder.Configuration);

    // CORS
    builder.Services.AddCors(opts =>
    {
        opts.AddDefaultPolicy(p => p
            .WithOrigins(
                "https://app.tutoria.tec.br",
                "https://app.dev.tutoria.tec.br",
                "https://tutoria-ui.vercel.app",
                "http://localhost:3000",
                "https://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    });

    var app = builder.Build();
    Log("Application built successfully");

    // =========================================================================
    // REQUEST LOGGING MIDDLEWARE - Logs every request with timing
    // =========================================================================
    app.Use(async (context, next) =>
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;
        var hasAuth = context.Request.Headers.ContainsKey("Authorization");

        try
        {
            await next(context);
            sw.Stop();

            var status = context.Response.StatusCode;
            var logLevel = status >= 500 ? "ERROR" : status >= 400 ? "WARN" : "INFO";
            Log($"[{logLevel}] {method} {path} -> {status} ({sw.ElapsedMilliseconds}ms) auth={hasAuth}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"[ERROR] {method} {path} -> EXCEPTION ({sw.ElapsedMilliseconds}ms)");
            Log($"  Type: {ex.GetType().Name}");
            Log($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                Log($"  Inner: {ex.InnerException.Message}");

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"error\": \"{ex.Message.Replace("\"", "'")}\"}}");
        }
    });

    // Swagger
    app.UseSwagger();
    app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "TutoriaFiles API v1"));

    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseCors();
    app.UseIpRateLimiting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Health check
    app.MapGet("/ping", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

    // Debug endpoints
    app.MapGet("/debug/log", () =>
    {
        if (File.Exists(logPath))
            return Results.Text(File.ReadAllText(logPath), "text/plain");
        return Results.Text("Log file not found", "text/plain");
    });

    app.MapGet("/debug/config", (IConfiguration config) => Results.Ok(new
    {
        environment = app.Environment.EnvironmentName,
        tutoriaApiUrl = config["TutoriaApi:BaseUrl"] ?? "(not set)",
        azureStorageConfigured = !string.IsNullOrWhiteSpace(config["AzureStorage:ConnectionString"]),
        databaseConfigured = !string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection"))
    }));

    Log("=== TutoriaFiles API Ready ===");
    app.Run();
}
catch (Exception ex)
{
    Log($"FATAL: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException != null)
        Log($"  Inner: {ex.InnerException.Message}");
    throw;
}
