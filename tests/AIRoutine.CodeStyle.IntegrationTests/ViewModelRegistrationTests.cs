using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class ViewModelRegistrationTests
{
    [Fact]
    public async Task ManualViewModelRegistration_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.ViewModelRegistration/Analyzers.ViewModelRegistration.csproj");

        // Assert
        Assert.True(result.Failed, $"Build should fail due to manual ViewModel DI registration. Output: {result.Output}");
        Assert.True(
            result.OutputContains("ACS0020"),
            $"Should contain ACS0020 error for manual ViewModel registration. Output: {result.Output}");
    }

    [Fact]
    public async Task NonViewModelServiceRegistration_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass/Analyzers.ViewMapRegistration/Analyzers.ViewMapRegistration.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed for non-ViewModel service registrations. Output: {result.Output}");
        Assert.False(
            result.OutputContains("ACS0020"),
            $"Should not contain ACS0020 for non-ViewModel registrations. Output: {result.Output}");
    }
}
