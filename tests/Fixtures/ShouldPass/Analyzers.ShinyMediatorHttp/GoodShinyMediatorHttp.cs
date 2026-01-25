using System.Net.Http;

namespace Analyzers.ShinyMediatorHttp;

/// <summary>
/// This file contains the correct Shiny Mediator HTTP patterns.
/// These should NOT trigger any ACS0019 errors.
///
/// Note: We mock the Shiny Mediator interfaces here to avoid package dependency
/// in the test fixture. The real implementation would use the Shiny.Mediator packages.
/// </summary>

#region Shiny Mediator Interface Mocks (for testing without package dependency)

// Mock interfaces that mimic Shiny Mediator
public interface IMediator
{
    Task<TResponse> Request<TResponse>(IRequest<TResponse> request);
}

public interface IRequest<TResponse> { }
public interface IHttpRequest<TResponse> : IRequest<TResponse> { }
public interface IMediatorContext { }

public interface IHttpRequestDecorator
{
    Task Decorate(HttpRequestMessage httpMessage, IMediatorContext context);
}

// Mock attributes that mimic Shiny Mediator HTTP
public enum HttpVerb { Get, Post, Put, Delete, Patch }
public enum HttpParameterType { Path, Query, Header }

[AttributeUsage(AttributeTargets.Class)]
public class HttpAttribute : Attribute
{
    public HttpAttribute(HttpVerb verb, string route) { }
}

[AttributeUsage(AttributeTargets.Property)]
public class HttpParameterAttribute : Attribute
{
    public HttpParameterAttribute(HttpParameterType type) { }
}

[AttributeUsage(AttributeTargets.Property)]
public class HttpBodyAttribute : Attribute { }

#endregion

// GOOD: HTTP request contract using IHttpRequest<T>
[Http(HttpVerb.Get, "/users/{UserId}")]
public class GetUserRequest : IHttpRequest<UserResponse>
{
    [HttpParameter(HttpParameterType.Path)]
    public string UserId { get; set; } = string.Empty;
}

[Http(HttpVerb.Post, "/users")]
public class CreateUserRequest : IHttpRequest<UserResponse>
{
    [HttpBody]
    public CreateUserPayload Body { get; set; } = new();
}

[Http(HttpVerb.Get, "/users")]
public class GetUsersRequest : IHttpRequest<List<UserResponse>>
{
    [HttpParameter(HttpParameterType.Query)]
    public int PageSize { get; set; } = 10;

    [HttpParameter(HttpParameterType.Query)]
    public int Page { get; set; } = 1;

    [HttpParameter(HttpParameterType.Header)]
    public string? Authorization { get; set; }
}

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CreateUserPayload
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Service that uses Shiny Mediator for HTTP calls.
/// </summary>
public class GoodUserService
{
    private readonly IMediator _mediator;

    // GOOD: Inject IMediator, not HttpClient
    public GoodUserService(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GOOD: Use mediator.Request() with IHttpRequest<T> contracts
    public async Task<UserResponse> GetUserAsync(string userId)
    {
        return await _mediator.Request(new GetUserRequest { UserId = userId });
    }

    public async Task<UserResponse> CreateUserAsync(string name, string email)
    {
        return await _mediator.Request(new CreateUserRequest
        {
            Body = new CreateUserPayload { Name = name, Email = email }
        });
    }

    public async Task<List<UserResponse>> GetUsersAsync(int page, int pageSize)
    {
        return await _mediator.Request(new GetUsersRequest
        {
            Page = page,
            PageSize = pageSize
        });
    }
}

/// <summary>
/// GOOD: IHttpRequestDecorator is allowed to work with HTTP internals.
/// This is the approved way to add authentication, logging, etc.
/// </summary>
public class AuthHttpRequestDecorator : IHttpRequestDecorator
{
    private readonly ITokenService _tokenService;

    public AuthHttpRequestDecorator(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    // GOOD: Decorators are allowed to access HttpRequestMessage
    public async Task Decorate(HttpRequestMessage httpMessage, IMediatorContext context)
    {
        var token = await _tokenService.GetAccessTokenAsync();
        httpMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}

public interface ITokenService
{
    Task<string> GetAccessTokenAsync();
}

/// <summary>
/// Regular service that doesn't use HTTP at all - should not trigger analyzer.
/// </summary>
public class RegularBusinessService
{
    private readonly IMediator _mediator;

    public RegularBusinessService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public string ProcessData(string input)
    {
        return input.ToUpperInvariant();
    }
}
