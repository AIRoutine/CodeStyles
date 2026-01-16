using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class UnoColorValidationTests
{
    [Fact]
    public async Task ValidColorsAndStyles_WithResourceDictionary_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass\Uno.ValidColors\Uno.ValidColors.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed with proper color resources and styles. Output: {result.Output}");
        Assert.False(result.OutputContains("hardcoded color"),
            "Should not report hardcoded color warnings");
        Assert.False(result.OutputContains("missing a Style"),
            "Should not report missing style warnings");
    }

    [Fact]
    public async Task HardcodedColorsXaml_WithInlineHexColors_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Uno.HardcodedColors.Xaml\Uno.HardcodedColors.Xaml.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail due to hardcoded colors in XAML");
        Assert.True(result.OutputContains("hardcoded color") || result.OutputContains("Hardcoded"),
            $"Should report hardcoded color error. Output: {result.Output}");
    }

    [Fact]
    public async Task HardcodedColorsCSharp_WithColorsClass_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Uno.HardcodedColors.CSharp\Uno.HardcodedColors.CSharp.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail due to hardcoded colors in C#");
        Assert.True(result.OutputContains("hardcoded color") || result.OutputContains("Hardcoded") || result.OutputContains("Colors."),
            $"Should report hardcoded color error. Output: {result.Output}");
    }

    [Fact]
    public async Task MissingStyles_WithoutStyleAttributes_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Uno.MissingStyles\Uno.MissingStyles.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail due to missing Style attributes");
        Assert.True(result.OutputContains("missing a Style") || result.OutputContains("Style attribute"),
            $"Should report missing style error. Output: {result.Output}");
    }
}
