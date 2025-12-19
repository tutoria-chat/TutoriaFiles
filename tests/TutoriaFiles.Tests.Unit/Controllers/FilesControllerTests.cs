using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TutoriaFiles.API.Controllers;
using TutoriaFiles.Core.DTOs;
using TutoriaFiles.Core.Entities;
using TutoriaFiles.Core.Interfaces;
using Xunit;
using FileEntity = TutoriaFiles.Core.Entities.File;

namespace TutoriaFiles.Tests.Unit.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<FilesController>> _loggerMock;
    private readonly FilesController _controller;
    private readonly User _testUser;

    public FilesControllerTests()
    {
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<FilesController>>();
        _controller = new FilesController(_fileServiceMock.Object, _loggerMock.Object);

        // Setup test user
        _testUser = new User
        {
            UserId = 1,
            Username = "testprofessor",
            Email = "test@example.com",
            UserType = "professor",
            UniversityId = 1,
            IsAdmin = false
        };

        // Setup controller context with claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testprofessor"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "professor"),
            new Claim("UniversityId", "1"),
            new Claim("isAdmin", "false")
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task UploadFile_ValidFile_ReturnsCreatedWithFileDetails()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test-document.pdf");
        fileMock.Setup(f => f.Length).Returns(1024 * 1024); // 1 MB
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));

        var request = new FileUploadRequest
        {
            ModuleId = 1,
            File = fileMock.Object,
            CustomName = "My Document"
        };

        var expectedFile = new FileEntity
        {
            Id = 1,
            Name = "My Document",
            FileType = "upload",
            FileName = "test-document.pdf",
            BlobPath = "universities/1/courses/1/modules/1/abc123.pdf",
            BlobUrl = "https://storage.blob.core.windows.net/tutoria-files/universities/1/courses/1/modules/1/abc123.pdf",
            ContentType = "application/pdf",
            FileSize = 1024 * 1024,
            ModuleId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _fileServiceMock
            .Setup(s => s.UploadFileAsync(
                request.ModuleId,
                It.IsAny<Stream>(),
                "test-document.pdf",
                "application/pdf",
                1024 * 1024,
                "My Document",
                It.IsAny<User>()))
            .ReturnsAsync(expectedFile);

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        var response = createdResult!.Value as FileDetailDto;

        response.Should().NotBeNull();
        response!.Id.Should().Be(1);
        response.Name.Should().Be("My Document");
        response.FileName.Should().Be("test-document.pdf");
        response.BlobUrl.Should().Contain("abc123.pdf");
    }

    [Fact]
    public async Task UploadFile_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new FileUploadRequest
        {
            ModuleId = 1,
            File = null!
        };

        _controller.ModelState.AddModelError("File", "File is required");

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadFile_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange - Remove claims to simulate no user
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var request = new FileUploadRequest
        {
            ModuleId = 1,
            File = fileMock.Object
        };

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UploadFile_ModuleNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var request = new FileUploadRequest
        {
            ModuleId = 999,
            File = fileMock.Object
        };

        _fileServiceMock
            .Setup(s => s.UploadFileAsync(
                It.IsAny<int>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new KeyNotFoundException("Module not found"));

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UploadFile_AccessDenied_ReturnsForbid()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var request = new FileUploadRequest
        {
            ModuleId = 1,
            File = fileMock.Object
        };

        _fileServiceMock
            .Setup(s => s.UploadFileAsync(
                It.IsAny<int>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have access to this module"));

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetFile_ValidId_ReturnsOk()
    {
        // Arrange
        var fileId = 1;

        _fileServiceMock
            .Setup(s => s.CanUserAccessFileAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.GetFile(fileId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFile_AccessDenied_ReturnsForbid()
    {
        // Arrange
        var fileId = 1;

        _fileServiceMock
            .Setup(s => s.CanUserAccessFileAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.GetFile(fileId);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetDownloadUrl_ValidId_ReturnsOkWithUrl()
    {
        // Arrange
        var fileId = 1;
        var expectedUrl = "https://storage.blob.core.windows.net/tutoria-files/uploads/test.pdf?sv=2021-08-06&...";

        _fileServiceMock
            .Setup(s => s.GetDownloadUrlAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _controller.GetDownloadUrl(fileId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDownloadUrl_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = 999;

        _fileServiceMock
            .Setup(s => s.GetDownloadUrlAsync(fileId, It.IsAny<User>()))
            .ThrowsAsync(new KeyNotFoundException("File not found"));

        // Act
        var result = await _controller.GetDownloadUrl(fileId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteFile_ValidId_ReturnsOk()
    {
        // Arrange
        var fileId = 1;

        _fileServiceMock
            .Setup(s => s.DeleteFileAsync(fileId, It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteFile_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = 999;

        _fileServiceMock
            .Setup(s => s.DeleteFileAsync(fileId, It.IsAny<User>()))
            .ThrowsAsync(new KeyNotFoundException("File not found"));

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void HealthCheck_ReturnsOk()
    {
        // Act
        var result = _controller.HealthCheck();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
    }
}
