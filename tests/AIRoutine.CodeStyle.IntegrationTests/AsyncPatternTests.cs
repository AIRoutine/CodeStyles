using Xunit;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed class AsyncPatternTests
{
    [Fact]
    public async Task ValidAsyncCode_ShouldBuildSuccessfully()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass/Analyzers.ValidAsync/Analyzers.ValidAsync.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed. Output: {result.Output}");
    }

    [Fact]
    public async Task AsyncVoid_NonEventHandler_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.AsyncPatterns/Analyzers.AsyncPatterns.csproj");

        // Assert
        Assert.True(result.Failed, $"Build should fail due to async void methods. Output: {result.Output}");
        Assert.True(
            result.OutputContains("ACS0009") || result.OutputContains("async void"),
            $"Should contain ACS0009 or async void error. Output: {result.Output}");
    }

    [Fact]
    public async Task BlockingCalls_Result_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.AsyncPatterns/Analyzers.AsyncPatterns.csproj");

        // Assert
        Assert.True(result.Failed, $"Build should fail due to blocking calls. Output: {result.Output}");
        Assert.True(
            result.OutputContains("ACS0010") || result.OutputContains(".Result") || result.OutputContains("deadlock"),
            $"Should contain ACS0010 or blocking call error. Output: {result.Output}");
    }

    [Fact]
    public async Task BlockingCalls_Wait_ShouldFailBuild()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.AsyncPatterns/Analyzers.AsyncPatterns.csproj");

        // Assert
        Assert.True(result.Failed, "Build should fail due to .Wait() calls");
        Assert.True(
            result.OutputContains("ACS0010") || result.OutputContains(".Wait()"),
            $"Should contain blocking call error for Wait(). Output: {result.Output}");
    }

    [Fact]
    public async Task FireAndForget_WithoutErrorHandling_ShouldWarn()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldFail/Analyzers.AsyncPatterns/Analyzers.AsyncPatterns.csproj");

        // Assert - Fire and forget is a warning, build might still succeed
        // but we should see the warning in the output
        Assert.True(
            result.OutputContains("ACS0011") || result.OutputContains("fire-and-forget") || result.OutputContains("not awaited"),
            $"Should contain fire-and-forget warning. Output: {result.Output}");
    }

    [Fact]
    public async Task AsyncVoid_EventHandler_ShouldPass()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass/Analyzers.ValidAsync/Analyzers.ValidAsync.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed for valid event handlers. Output: {result.Output}");
        Assert.False(
            result.OutputContains("ACS0009"),
            $"Should not contain ACS0009 for event handlers. Output: {result.Output}");
    }

    [Fact]
    public async Task Discard_WithComment_ShouldPass()
    {
        // Arrange & Act
        var result = await BuildTestRunner.RestoreAndBuildProjectAsync(
            @"ShouldPass/Analyzers.ValidAsync/Analyzers.ValidAsync.csproj");

        // Assert
        Assert.True(result.Succeeded, $"Build should succeed for discarded tasks. Output: {result.Output}");
    }
}
