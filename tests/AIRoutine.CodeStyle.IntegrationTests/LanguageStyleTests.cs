using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class LanguageStyleTests
{
    [Fact]
    public async Task BadStyle_WithBlockNamespace_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Common.BadStyle\Common.BadStyle.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail due to style violations");

        // Check for style-related diagnostics
        var hasStyleViolation = result.HasDiagnostic("IDE0161") || // File-scoped namespace
                                result.HasDiagnostic("IDE0005") || // Unnecessary using
                                result.HasDiagnostic("IDE0040");   // Add accessibility modifier

        Assert.True(hasStyleViolation,
            $"Should have style violation diagnostics. Found: {string.Join(", ", result.DiagnosticIds)}");
    }
}
