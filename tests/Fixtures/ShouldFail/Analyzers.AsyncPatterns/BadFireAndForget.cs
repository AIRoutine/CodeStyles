namespace Analyzers.AsyncPatterns;

/// <summary>
/// This file contains fire-and-forget async calls without error handling.
/// These should trigger ACS0011 warnings.
/// </summary>
public class BadFireAndForget
{
    // BAD: Unawaited task without error handling
    public void ProcessWithoutAwaiting()
    {
        DoWorkAsync(); // ACS0011 - fire and forget
    }

    // BAD: Multiple unawaited tasks
    public void ProcessMultipleWithoutAwaiting()
    {
        DoWorkAsync(); // ACS0011
        SaveDataAsync("test"); // ACS0011
    }

    private Task DoWorkAsync()
    {
        return Task.Delay(100);
    }

    private Task SaveDataAsync(string data)
    {
        return Task.Delay(100);
    }
}
