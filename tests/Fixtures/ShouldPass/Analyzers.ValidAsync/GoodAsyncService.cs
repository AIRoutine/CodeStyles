namespace Analyzers.ValidAsync;

/// <summary>
/// This file contains valid async patterns that should NOT trigger any diagnostics.
/// </summary>
public class GoodAsyncService
{
    // GOOD: async Task method
    public async Task ProcessDataAsync()
    {
        await Task.Delay(100);
    }

    // GOOD: async Task<T> method
    public async Task<string> GetDataAsync()
    {
        await Task.Delay(100);
        return "data";
    }

    // GOOD: Proper await usage
    public async Task ProcessWithAwait()
    {
        await DoWorkAsync();
        var result = await GetDataAsync();
    }

    // GOOD: Explicit discard with comment
    public void ProcessWithDiscard()
    {
        _ = DoWorkAsync(); // Fire and forget - exceptions are intentionally ignored
    }

    // GOOD: Using ContinueWith for error handling
    public void ProcessWithErrorHandling()
    {
        _ = DoWorkAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Console.WriteLine(t.Exception);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    // GOOD: Result in static constructor (allowed context)
    private static readonly string s_data = Task.FromResult("static").Result;

    // GOOD: async void event handler pattern
    public async void Button_Click(object sender, EventArgs e)
    {
        await Task.Delay(100);
    }

    // GOOD: async void with EventArgs-derived parameter
    public async void OnNavigated(object sender, NavigationEventArgs e)
    {
        await Task.Delay(100);
    }

    private Task DoWorkAsync()
    {
        return Task.Delay(100);
    }
}

// Simulated EventArgs for testing
public class NavigationEventArgs : EventArgs
{
}
