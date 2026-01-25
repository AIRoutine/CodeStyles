using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class ExtensionMethodTests
{
    [Fact]
    public async Task ClassicExtensionMethod_ShouldTriggerWarning()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.ClassicExtensions/Analyzers.ClassicExtensions.csproj");

        // Assert - The analyzer produces warnings (not errors by default)
        Assert.True(
            result.OutputContains("ACS0018") || result.OutputContains("extension block syntax"),
            $"Should contain ACS0018 warning for classic extension methods. Output: {result.Output}");
    }

    [Fact]
    public async Task ModernExtensionBlock_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass/Analyzers.ModernExtensions/Analyzers.ModernExtensions.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed for modern extension syntax. Output: {result.Output}");
        Assert.False(
            result.OutputContains("ACS0018"),
            $"Should not contain ACS0018 for modern extension syntax. Output: {result.Output}");
    }
}
