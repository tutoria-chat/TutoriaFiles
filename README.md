# TutoriaFiles API

Dedicated file upload/download API for the Tutoria platform. Built with .NET 8 and designed to handle large file uploads (up to 15MB) to Azure Blob Storage.

## Architecture

This API follows the **Onion Architecture** pattern with clean separation of concerns:

```
TutoriaFiles/
├── src/
│   ├── TutoriaFiles.Core/           # Domain entities, interfaces, DTOs
│   ├── TutoriaFiles.Infrastructure/ # Azure Blob Storage, services
│   └── TutoriaFiles.API/            # Web API endpoints
└── tests/
    └── TutoriaFiles.Tests.Unit/     # Unit tests
```

## Features

- ✅ **Large File Uploads** - Handles up to 15MB file uploads
- ✅ **Azure Blob Storage** - Secure cloud file storage
- ✅ **JWT Authentication** - Optional token validation from TutoriaApi
- ✅ **Rate Limiting** - Protects against abuse
- ✅ **CORS Support** - Cross-origin requests enabled
- ✅ **Automatic DI** - Services auto-registered via reflection
- ✅ **Swagger/OpenAPI** - Interactive API documentation

## Prerequisites

- .NET 8 SDK
- Azure Storage Account
- (Optional) Access to TutoriaApi JWT secret for authentication

## Configuration

### appsettings.json

```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "tutoria-files"
  },
  "Jwt": {
    "SecretKey": "YourSecretKeyHere (min 32 chars)",
    "Issuer": "TutoriaAuthApi",
    "Audience": "TutoriaApi"
  }
}
```

### Environment Variables (Production)

- `AZURE_STORAGE_CONNECTION_STRING`
- `JWT_SECRET_KEY`

## Running Locally

```bash
# Restore dependencies
dotnet restore

# Run the API
cd src/TutoriaFiles.API
dotnet run

# API will be available at:
# http://localhost:5000
# https://localhost:5001
```

## API Endpoints

### Upload File
```http
POST /api/files/upload
Content-Type: multipart/form-data

file: <binary>
customName: "optional-custom-name.pdf" (optional)
```

**Response:**
```json
{
  "fileName": "document.pdf",
  "blobPath": "uploads/2025-12-18/abc123-def456.pdf",
  "blobUrl": "https://storage.blob.core.windows.net/tutoria-files/...",
  "contentType": "application/pdf",
  "fileSize": 1048576,
  "uploadedAt": "2025-12-18T10:30:00Z"
}
```

### Get Download URL
```http
GET /api/files/download?blobPath=uploads/2025-12-18/abc123-def456.pdf&expiresInHours=1
```

**Response:**
```json
{
  "downloadUrl": "https://storage.blob.core.windows.net/tutoria-files/...?sv=2021-08-06&...",
  "expiresInHours": 1
}
```

### Delete File
```http
DELETE /api/files/delete?blobPath=uploads/2025-12-18/abc123-def456.pdf
```

**Response:**
```json
{
  "message": "File deleted successfully"
}
```

### Health Check
```http
GET /api/files/health
```

**Response:**
```json
{
  "status": "healthy",
  "service": "TutoriaFiles.API",
  "timestamp": "2025-12-18T10:30:00Z"
}
```

## Authentication

### JWT Bearer Token (Optional)

If JWT authentication is configured, include the Authorization header:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Public Mode

If `Jwt:SecretKey` is not configured, the API runs in public mode (no authentication required). **Use with caution!**

## Deployment

### Azure App Service

1. **Create Azure App Service** for .NET 8
2. **Configure Application Settings**:
   - `AZURE_STORAGE_CONNECTION_STRING`
   - `JWT_SECRET_KEY` (if using authentication)
3. **Deploy**:
   ```bash
   dotnet publish -c Release
   # Deploy the contents of bin/Release/net8.0/publish/
   ```

### Docker (Future)

```bash
# Build image
docker build -t tutoria-files-api .

# Run container
docker run -p 5000:8080 -e AZURE_STORAGE_CONNECTION_STRING="..." tutoria-files-api
```

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Rate Limiting

Default rate limits (can be customized in appsettings.json):

- **General**: 60 requests/minute, 500 requests/hour
- **Upload**: 10 uploads/minute

## File Size Limits

- **Maximum file size**: 15 MB (15,728,640 bytes)
- Configured at multiple levels:
  - Kestrel server limits
  - FormOptions limits
  - Controller attribute limits

## Security Considerations

- ✅ File size validation
- ✅ Filename sanitization
- ✅ Rate limiting
- ✅ CORS restrictions
- ✅ Optional JWT authentication
- ⚠️ File type validation (can be enabled via `FileHelper.IsAllowedFileType`)

## Troubleshooting

### "413 Request Entity Too Large"

- Check Kestrel limits in Program.cs
- Verify FormOptions configuration
- Ensure reverse proxy (if any) allows large uploads

### "Azure Storage connection failed"

- Verify connection string format
- Check Azure Storage account exists
- Ensure container exists (auto-created if missing)

### "JWT token invalid"

- Verify JWT secret matches TutoriaApi
- Check token expiration
- Ensure Issuer and Audience match

## Contributing

See `CLAUDE.md` for development guidelines and coding standards.

## License

Proprietary - Tutoria Platform
