using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class AspNetCoreRelaxationTests
{
    [Fact]
    public async Task ValidCode_WithoutConfigureAwait_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass\AspNetCore.ValidCode\AspNetCore.ValidCode.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed without ConfigureAwait. Output: {result.Output}");

        // Ensure CA2007 (ConfigureAwait) is not triggered
        Assert.False(result.HasDiagnostic("CA2007"),
            "CA2007 (ConfigureAwait) should be disabled for ASP.NET Core projects");
    }
}
