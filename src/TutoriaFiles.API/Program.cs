using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TutoriaFiles.Infrastructure;
using TutoriaFiles.API.Authentication;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to allow large file uploads (15MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 15728640; // 15 MB in bytes (15 * 1024 * 1024)
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5); // 5 minutes for slow connections
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5); // 5 minutes keep-alive
});

// Configure built-in logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

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

// Configure JWT Authentication with TutoriaApi validation + local fallback
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];
var tutoriaApiUrl = builder.Configuration["TutoriaApi:BaseUrl"];

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

    if (!string.IsNullOrWhiteSpace(tutoriaApiUrl))
    {
        Console.WriteLine($"✓ JWT Authentication configured (TutoriaApi: {tutoriaApiUrl} with local fallback)");
    }
    else
    {
        Console.WriteLine("✓ JWT Authentication configured (local validation only)");
    }
}
else
{
    // No authentication - all endpoints are open (use with caution!)
    Console.WriteLine("⚠ WARNING: No JWT authentication configured - API is public!");
}

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

// Add Infrastructure services (Blob Storage, Services) - automatically registered!
builder.Services.AddInfrastructure(builder.Configuration);

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

var app = builder.Build();

// Configure the HTTP request pipeline
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

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 TutoriaFiles API starting...");
logger.LogInformation("📦 File Upload API: /api/files/upload (max 15MB)");
logger.LogInformation("📥 File Download API: /api/files/download");
logger.LogInformation("🗑️ File Delete API: /api/files/delete");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.Run();
