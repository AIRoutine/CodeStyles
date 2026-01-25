using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Analyzers.HttpClientUsage;

/// <summary>
/// This file contains forbidden HttpClient usage patterns.
/// All HTTP calls should go through Shiny Mediator instead.
/// These should trigger ACS0019 errors.
/// </summary>
public class BadHttpClientService
{
    // BAD: HttpClient as constructor parameter
    private readonly HttpClient _httpClient;

    public BadHttpClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // BAD: Direct HttpClient method calls
    public async Task<string> GetDataAsync()
    {
        var response = await _httpClient.GetAsync("https://api.example.com/data");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task PostDataAsync(string data)
    {
        await _httpClient.PostAsync("https://api.example.com/data", new StringContent(data));
    }

    public async Task<string> GetStringDirectlyAsync()
    {
        return await _httpClient.GetStringAsync("https://api.example.com/data");
    }
}

/// <summary>
/// Service that creates HttpClient directly.
/// </summary>
public class BadHttpClientInstantiation
{
    // BAD: Creating HttpClient directly
    public async Task<string> FetchDataAsync()
    {
        var client = new HttpClient();
        var response = await client.GetAsync("https://api.example.com");
        return await response.Content.ReadAsStringAsync();
    }

    // BAD: Using implicit new with HttpClient
    public HttpClient CreateClient()
    {
        HttpClient client = new();
        return client;
    }
}

/// <summary>
/// Service that uses IHttpClientFactory.
/// </summary>
public class BadHttpClientFactoryUsage
{
    // BAD: IHttpClientFactory as constructor parameter
    private readonly IHttpClientFactory _factory;

    public BadHttpClientFactoryUsage(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<string> GetDataAsync()
    {
        var client = _factory.CreateClient();
        return await client.GetStringAsync("https://api.example.com");
    }
}

/// <summary>
/// Service registration that uses AddHttpClient.
/// </summary>
public static class BadServiceRegistration
{
    // BAD: Using AddHttpClient in DI setup
    public static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<BadHttpClientService>();
        return services;
    }
}

/// <summary>
/// Class with HttpClient as field.
/// </summary>
public class BadHttpClientField
{
    // BAD: HttpClient as field
    private HttpClient _client = new HttpClient();

    public async Task DoWork()
    {
        await _client.GetAsync("https://api.example.com");
    }
}

/// <summary>
/// Class with HttpClient as property.
/// </summary>
public class BadHttpClientProperty
{
    // BAD: HttpClient as property
    public HttpClient Client { get; set; } = new HttpClient();
}
