using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class HttpClientUsageTests
{
    [Fact]
    public async Task DirectHttpClientUsage_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.HttpClientUsage/Analyzers.HttpClientUsage.csproj");

        // Assert
        Assert.True(result.Failed, $"Build should fail due to direct HttpClient usage. Output: {result.Output}");
        Assert.True(
            result.OutputContains("ACS0019"),
            $"Should contain ACS0019 error for HttpClient usage. Output: {result.Output}");
    }

    [Fact]
    public async Task ShinyMediatorHttpUsage_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass/Analyzers.ShinyMediatorHttp/Analyzers.ShinyMediatorHttp.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed for Shiny Mediator HTTP usage. Output: {result.Output}");
        Assert.False(
            result.OutputContains("ACS0019"),
            $"Should not contain ACS0019 for Shiny Mediator usage. Output: {result.Output}");
    }
}
