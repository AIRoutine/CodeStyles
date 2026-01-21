namespace Analyzers.AsyncPatterns;

/// <summary>
/// This file contains async void methods that are NOT event handlers.
/// These should trigger ACS0009 errors.
/// </summary>
public class BadAsyncVoidService
{
    // BAD: async void method that is not an event handler
    public async void ProcessDataAsync()
    {
        await Task.Delay(100);
    }

    // BAD: async void method that is not an event handler
    public async void SaveAsync(string data)
    {
        await Task.Delay(100);
    }

    // BAD: async void local function
    public void DoWork()
    {
        async void LocalAsyncVoid()
        {
            await Task.Delay(100);
        }

        LocalAsyncVoid();
    }
}
