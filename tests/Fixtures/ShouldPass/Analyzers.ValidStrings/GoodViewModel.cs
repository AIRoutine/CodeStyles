using System;
using Microsoft.Extensions.Logging;

namespace Analyzers.ValidStrings;

/// <summary>
/// This ViewModel contains only allowed string patterns.
/// </summary>
public class GoodViewModel
{
    private readonly ILogger<GoodViewModel> _logger;

    // OK: Constant declaration
    private const string DefaultTitle = "Welcome";

    public GoodViewModel(ILogger<GoodViewModel> logger)
    {
        _logger = logger;
    }

    // OK: Empty strings
    public string EmptyValue { get; set; } = "";
    public string WhitespaceValue { get; set; } = "   ";

    // OK: Single character (separator)
    public string Separator => ",";

    // OK: nameof expression context (the parameter name is from nameof)
    public void ValidateInput(string input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
    }

    // OK: Logging format strings with placeholders
    public void LogUserAction(string userId, string action)
    {
        _logger.LogInformation("User {UserId} performed action {Action}", userId, action);
        _logger.LogDebug("Processing request for {User}", userId);
    }

    // OK: Technical strings - URLs
    public string ApiEndpoint => "https://api.example.com/v1/users";

    // OK: Technical strings - Paths
    public string ConfigPath => "/etc/app/config.json";

    // OK: Technical strings - MIME types
    public string ContentType => "application/json";

    // OK: Technical strings - Date format
    public string DateFormat => "yyyy-MM-dd HH:mm:ss";

    // OK: Simple identifiers (property keys)
    public string SettingsKey => "UserPreferences";
    public string PropertyName => "IsEnabled";

    // OK: Attribute parameters
    [Obsolete("Use NewMethod instead")]
    public void OldMethod() { }

    // OK: Dot-separated identifiers
    public string ConfigSection => "Application.Settings.Theme";
}
