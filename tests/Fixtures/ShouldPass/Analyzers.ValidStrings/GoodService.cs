using System;
using Microsoft.Extensions.Logging;

namespace Analyzers.ValidStrings;

/// <summary>
/// This Service contains only allowed string patterns.
/// </summary>
public class GoodService
{
    private readonly ILogger<GoodService> _logger;

    // OK: Constants are allowed
    private const string ConnectionStringKey = "DefaultConnection";
    public const string ServiceName = "UserService";

    public GoodService(ILogger<GoodService> logger)
    {
        _logger = logger;
    }

    // OK: Resource accessor pattern (method name indicates localization)
    public string GetLocalizedString(string key)
    {
        // The key parameter is an identifier, allowed
        return key;
    }

    // OK: Logging with structured placeholders
    public void ProcessOrder(int orderId, string customerName)
    {
        _logger.LogInformation("Processing order {OrderId} for customer {CustomerName}", orderId, customerName);

        try
        {
            // Process...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", orderId);
            throw;
        }
    }

    // OK: API route (technical string)
    public string GetApiRoute() => "api/users/{id}";

    // OK: Connection string pattern (technical)
    public string GetConnectionString() => "Server=localhost;Database=AppDb;Trusted_Connection=True;";

    // OK: Regex pattern (technical)
    public string EmailPattern => @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$";

    // OK: Simple identifier for dictionary key
    public void SetSetting(string value)
    {
        var settings = new System.Collections.Generic.Dictionary<string, string>
        {
            ["Theme"] = value,
            ["Language"] = value
        };
        _ = settings;
    }
}
