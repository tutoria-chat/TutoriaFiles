using FluentAssertions;
using TutoriaFiles.Infrastructure.Helpers;
using Xunit;

namespace TutoriaFiles.Tests.Unit.Helpers;

public class FileHelperTests
{
    [Theory]
    [InlineData("my document.pdf", "my_document.pdf")]
    [InlineData("test file (1).docx", "test_file_1.docx")]
    [InlineData("special!@#$%chars.txt", "specialchars.txt")]
    [InlineData("múltiple  spaces.pdf", "mltiple__spaces.pdf")] // Double space becomes double underscore
    public void SanitizeFilename_VariousInputs_SanitizesCorrectly(string input, string expected)
    {
        // Act
        var result = FileHelper.SanitizeFilename(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeFilename_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = FileHelper.SanitizeFilename(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeFilename_Null_ReturnsEmpty()
    {
        // Act
        var result = FileHelper.SanitizeFilename(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeFilename_VeryLongFilename_TruncatesToLimit()
    {
        // Arrange
        var longName = new string('a', 300) + ".pdf";

        // Act
        var result = FileHelper.SanitizeFilename(longName);

        // Assert
        result.Length.Should().BeLessOrEqualTo(255);
        result.Should().EndWith(".pdf");
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/msword", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("text/plain", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("video/mp4", true)]
    [InlineData("audio/mpeg", true)]
    [InlineData("application/octet-stream", false)]
    [InlineData("application/x-executable", false)]
    [InlineData("text/html", false)]
    public void IsAllowedFileType_VariousContentTypes_ReturnsCorrectResult(string contentType, bool expectedResult)
    {
        // Act
        var result = FileHelper.IsAllowedFileType(contentType);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("document.pdf", "pdf")]
    [InlineData("presentation.PPTX", "pptx")]
    [InlineData("image.PNG", "png")]
    [InlineData("no-extension", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void GetFileExtension_VariousInputs_ReturnsCorrectExtension(string? filename, string expectedExtension)
    {
        // Act
        var result = FileHelper.GetFileExtension(filename!);

        // Assert
        result.Should().Be(expectedExtension);
    }

    [Fact]
    public void GetFileExtension_MultipleDotsInFilename_ReturnsLastExtension()
    {
        // Arrange
        var filename = "my.backup.file.tar.gz";

        // Act
        var result = FileHelper.GetFileExtension(filename);

        // Assert
        result.Should().Be("gz");
    }
}
