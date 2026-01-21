namespace Analyzers.AsyncPatterns;

/// <summary>
/// This file contains blocking calls on Tasks.
/// These should trigger ACS0010 errors.
/// </summary>
public class BadBlockingCalls
{
    // BAD: Using .Result
    public string GetDataBlocking()
    {
        var task = GetDataAsync();
        return task.Result; // ACS0010
    }

    // BAD: Using .Wait()
    public void WaitForCompletion()
    {
        var task = GetDataAsync();
        task.Wait(); // ACS0010
    }

    // BAD: Using .GetAwaiter().GetResult()
    public string GetDataWithGetResult()
    {
        var task = GetDataAsync();
        return task.GetAwaiter().GetResult(); // ACS0010
    }

    // BAD: Using Task.WaitAll
    public void WaitForMultiple()
    {
        var task1 = GetDataAsync();
        var task2 = GetDataAsync();
        Task.WaitAll(task1, task2); // ACS0010
    }

    private Task<string> GetDataAsync()
    {
        return Task.FromResult("data");
    }
}
