using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class StaticCallsTests
{
    [Fact]
    public async Task ValidCode_WithOnlySystemStaticCalls_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass\Common.ValidCode\Common.ValidCode.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed. Output: {result.Output}");
    }

    [Fact]
    public async Task BadStaticUsage_WithCustomStaticCalls_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Common.StaticCalls\Common.StaticCalls.csproj");

        // Assert
        Assert.True(result.Failed, $"Build should fail due to static call violations. Output: {result.Output}");
        Assert.True(
            result.OutputContains("Static call") && result.OutputContains("is not allowed"),
            $"Should contain static call error message. Output: {result.Output}");
    }

    [Fact]
    public async Task BadStaticUsage_ShouldReportMyHelperViolation()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Common.StaticCalls\Common.StaticCalls.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail");
        Assert.True(
            result.OutputContains("MyHelper.Calculate"),
            $"Should report MyHelper.Calculate violation. Output: {result.Output}");
    }

    [Fact]
    public async Task BadStaticUsage_ShouldReportStringUtilsViolation()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Common.StaticCalls\Common.StaticCalls.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail");
        Assert.True(
            result.OutputContains("StringUtils.Format"),
            $"Should report StringUtils.Format violation. Output: {result.Output}");
    }
}
