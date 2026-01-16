using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class NamingConventionTests
{
    [Fact]
    public async Task ValidCode_WithCorrectNaming_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass\Common.ValidCode\Common.ValidCode.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed. Output: {result.Output}");
    }

    [Fact]
    public async Task BadNaming_WithInterfaceWithoutIPrefix_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail\Common.BadNaming\Common.BadNaming.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail due to naming violations");
        Assert.True(result.HasDiagnostic("IDE1006"), $"Should have IDE1006 (naming rule violation). Diagnostics: {string.Join(", ", result.DiagnosticIds)}");
    }
}
