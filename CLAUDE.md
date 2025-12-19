# TutoriaFiles API - Development Guidelines

## Technology Stack
- **Framework**: .NET 8
- **Storage**: Azure Blob Storage
- **Authentication**: JWT Bearer tokens (optional)
- **Architecture**: Onion Architecture (Clean Architecture)

## Architecture

### Onion Architecture
```
TutoriaFiles/
├── src/
│   ├── TutoriaFiles.Core/           # Domain layer (DTOs, Interfaces)
│   ├── TutoriaFiles.Infrastructure/ # Infrastructure layer (Azure Blob, Services)
│   └── TutoriaFiles.API/            # Presentation layer (Controllers, Middleware)
```

### Design Patterns
- **Service Pattern**: Business logic in services
- **Dependency Injection**: Automatic DI registration via reflection
- **Factory Pattern**: Blob path generation

### Separation of Concerns - CRITICAL RULES

**Controllers (Lean & Simple)**:
- Validate request DTOs (ModelState, manual validation)
- Call service methods **ALWAYS wrapped in try-catch blocks**
- Map service results to HTTP responses (Ok, BadRequest, NotFound, etc.)
- **NO business logic**
- **NO direct service calls without exception handling**

**CRITICAL: Exception Handling in Controllers**
- **EVERY service call MUST be wrapped in try-catch**
- Handle expected exceptions (InvalidOperationException, etc.)
- Log unexpected exceptions with full details
- Return appropriate HTTP status codes (404, 400, 500)
- NEVER let exceptions bubble up to the client unhandled

**Services (Business Logic)**:
- Contain all business logic and orchestration
- Validate business rules (file size, filename sanitization, etc.)
- Handle Azure Blob Storage operations
- Transform data between DTOs
- **NO HTTP concerns** (no StatusCode, no ActionResult)

**Example - GOOD Architecture**:
```csharp
// Service - Business logic only
public class BlobStorageService : IBlobStorageService
{
    public async Task<string> UploadFileAsync(Stream fileStream, string blobPath, string contentType)
    {
        // Business logic: upload to Azure Blob
        var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(blobPath);
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } });
        return blobClient.Uri.ToString();
    }
}

// Controller - Lean & simple with proper error handling
public class FilesController : ControllerBase
{
    [HttpPost("upload")]
    public async Task<ActionResult<FileUploadResponse>> UploadFile([FromForm] FileUploadRequest request)
    {
        try
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { message = "File is required" });

            var blobUrl = await _blobStorageService.UploadFileAsync(stream, blobPath, contentType);
            return Ok(new FileUploadResponse { BlobUrl = blobUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during file upload");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file");
            return StatusCode(500, new { message = "An error occurred while uploading the file" });
        }
    }
}
```

## Unit Testing Requirements

### MANDATORY: All New Features Must Have Unit Tests

**Test Coverage Requirements:**
- ✅ **Service Tests**: Mock Azure Blob Storage operations
- ✅ **Controller Tests**: Mock service dependencies
- ✅ Test all success paths and error scenarios
- ✅ Test file size validation
- ✅ Test filename sanitization
- ✅ Test exception handling

**Test Project Location:**
- `TutoriaFiles/tests/TutoriaFiles.Tests.Unit/`

**Testing Framework:**
- **XUnit**: Test framework
- **Moq**: Mocking library
- **FluentAssertions**: Assertion library

### Service Unit Tests

**Purpose:** Test business logic without hitting Azure Blob Storage.

**Example:**
```csharp
public class BlobStorageServiceTests
{
    private readonly Mock<BlobServiceClient> _blobServiceClientMock;
    private readonly Mock<ILogger<BlobStorageService>> _loggerMock;
    private readonly BlobStorageService _service;

    [Fact]
    public async Task UploadFileAsync_ValidFile_ReturnsUrl()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var blobPath = "uploads/test.pdf";

        // Act
        var result = await _service.UploadFileAsync(stream, blobPath, "application/pdf");

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain(blobPath);
    }
}
```

### Controller Unit Tests

**Purpose:** Test HTTP endpoint behavior.

**Example:**
```csharp
public class FilesControllerTests
{
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock;
    private readonly Mock<ILogger<FilesController>> _loggerMock;
    private readonly FilesController _controller;

    [Fact]
    public async Task UploadFile_ValidFile_ReturnsOk()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var request = new FileUploadRequest { File = fileMock.Object };

        _blobStorageServiceMock
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.url/test.pdf");

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
```

## Running the API

### IMPORTANT: DO NOT Run as Background Task
**NEVER run .NET APIs as background tasks** using `dotnet run` with `run_in_background: true`.

**Why:**
- File locking issues (DLL conflicts)
- Port conflicts
- User may already have Visual Studio running the API

**What to do instead:**
- For build verification, use `dotnet build` (NOT `dotnet run`)
- Only run `dotnet test` for unit tests
- Let the user manage API instances

## Configuration & Secrets Management

### appsettings.json Configuration Structure

**Local Development** (`appsettings.json` / `appsettings.Development.json`):
- Contains placeholder values
- Safe to commit to source control

**Production Configuration** (Azure App Service settings):
- **NEVER committed to source control**
- Configured via Azure App Service Application Settings
- Overrides appsettings.json values

### Required Configuration Sections

```json
{
  "AzureStorage": {
    "ConnectionString": "Azure Storage connection string",
    "ContainerName": "tutoria-files"
  },
  "Jwt": {
    "SecretKey": "Your secret key (min 32 chars)",
    "Issuer": "TutoriaAuthApi",
    "Audience": "TutoriaApi"
  },
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [...]
  }
}
```

## Code Standards

### Naming Conventions
- **C# Properties**: PascalCase (e.g., `FileName`, `BlobPath`, `ContentType`)
- **JSON Properties**: camelCase (e.g., `fileName`, `blobPath`, `contentType`) - automatic via ASP.NET Core
- **API Endpoints**: `/api/[controller]/[action]` pattern
- **NO snake_case**: We use PascalCase/camelCase only

### DTOs and Mapping
- **Always use DTOs** for API requests and responses
- **Validation**: Use Data Annotations on DTOs (`[Required]`, `[MaxLength]`, etc.)
- **Naming**: Request DTOs end with `Request`, Response DTOs end with `Response`

### Namespace Pattern
Use file-scoped namespace declaration:
```csharp
namespace TutoriaFiles.Core.DTOs;

public class FileUploadRequest
{
    // class implementation
}
```

### Project Structure
- **Core**: DTOs, interfaces
- **Infrastructure**: Service implementations (Azure Blob)
- **API**: Controllers, Program.cs, middleware

### Automatic Dependency Injection

**How it works:**
- All service interfaces (`I*Service`) in `Core.Interfaces` are automatically registered
- Implementations are auto-discovered from `Infrastructure.Services`
- Matching is done by interface name (e.g., `IBlobStorageService` → `BlobStorageService`)
- All registered as Scoped lifetime

**Implementation:** See `TutoriaFiles.Infrastructure/DependencyInjection.cs`

**Example:**
```csharp
// 1. Create interface in Core
public interface IMyService
{
    Task DoSomethingAsync();
}

// 2. Create implementation in Infrastructure
public class MyService : IMyService
{
    public async Task DoSomethingAsync() { /* implementation */ }
}

// 3. That's it! No Program.cs changes needed
// On startup you'll see:
// ✓ Registered: IMyService → MyService
```

## Security Considerations

### File Upload Security
- ✅ File size validation (15MB limit)
- ✅ Filename sanitization (remove unsafe characters)
- ✅ Rate limiting (10 uploads/minute)
- ⚠️ File type validation (can be enabled via `FileHelper.IsAllowedFileType`)

### Authentication
- ✅ Optional JWT Bearer token validation
- ✅ Tokens validated against TutoriaApi secret
- ⚠️ Public mode available (no authentication) - use with caution

### CORS
- ✅ Restricted to specific origins
- ✅ AllowCredentials enabled
- ✅ AllowAnyMethod and AllowAnyHeader

## Performance Optimization

### File Upload Performance
- Kestrel configured for 15MB uploads
- FormOptions configured for multipart uploads
- Timeout extended to 5 minutes for slow connections

### Rate Limiting
- Protects against abuse
- Configurable per endpoint
- In-memory cache for rate limit tracking

## Deployment to Azure App Service

### Prerequisites
1. Azure Storage Account created
2. Azure App Service created (.NET 8)
3. Application Settings configured

### Application Settings (Azure)
```
AZURE_STORAGE_CONNECTION_STRING = "DefaultEndpointsProtocol=https;..."
JWT_SECRET_KEY = "YourProductionSecretKey"
```

### Deployment Steps
```bash
# Build for release
dotnet publish -c Release

# Deploy contents of bin/Release/net8.0/publish/
# Use Azure DevOps, GitHub Actions, or manual FTP upload
```

### Azure-Specific Configuration
- **Kestrel Limits**: Pre-configured for 15MB uploads
- **Logging**: Console logs sent to Azure App Service logs
- **Health Checks**: `/ping` endpoint for load balancer

## Troubleshooting Guide

### Common Issues

#### Issue: "413 Request Entity Too Large"
**Solution**:
- Check Kestrel limits in Program.cs (line 13-16)
- Verify FormOptions configuration (line 35-40)
- If using reverse proxy, check proxy settings

#### Issue: "Azure Storage connection failed"
**Solution**:
- Verify connection string format
- Check Azure Storage account exists
- Ensure container name is correct
- Container auto-created if missing

#### Issue: "JWT token invalid"
**Solution**:
- Verify JWT secret matches TutoriaApi
- Check token expiration
- Ensure Issuer and Audience match

## Testing Strategy

### Unit Tests
- Mock Azure Blob Storage operations
- Test business logic in services
- Test HTTP responses in controllers
- Test exception handling

### Integration Tests (Future)
- Test actual Azure Blob Storage uploads
- Test end-to-end file upload flow
- Test file download with SAS tokens

### Manual Testing
- Use Swagger UI for interactive testing
- Test with Postman/Insomnia
- Test different file sizes and types

## Best Practices

### DO:
- ✅ Always validate file size
- ✅ Sanitize filenames
- ✅ Use try-catch in controllers
- ✅ Log errors with context
- ✅ Return appropriate HTTP status codes
- ✅ Use DTOs for all API communication

### DON'T:
- ❌ Skip exception handling
- ❌ Allow unlimited file uploads
- ❌ Trust user-provided filenames
- ❌ Return stack traces to clients
- ❌ Run as background task during development
- ❌ Commit production secrets to source control
