namespace AspNetCore.ValidCode.Services;

public interface IDataService
{
    Task<string> GetDataAsync();
    Task<int> GetCountAsync();
}

public sealed class DataService : IDataService
{
    private readonly HttpClient _httpClient;

    public DataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // GOOD: No ConfigureAwait required in ASP.NET Core (CA2007 is disabled)
    public async Task<string> GetDataAsync()
    {
        var response = await _httpClient.GetAsync("https://api.example.com/data");
        return await response.Content.ReadAsStringAsync();
    }

    // GOOD: Multiple awaits without ConfigureAwait
    public async Task<int> GetCountAsync()
    {
        await Task.Delay(100);
        var data = await GetDataAsync();
        return data.Length;
    }
}
