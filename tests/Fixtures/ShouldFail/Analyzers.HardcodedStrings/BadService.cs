namespace Analyzers.HardcodedStrings;

/// <summary>
/// This Service contains hardcoded strings that should trigger ACS0001.
/// </summary>
public class BadService
{
    // ACS0001: Hardcoded exception message
    public void ProcessData(object? data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data), "Data cannot be null");

        // ACS0001: Hardcoded user-facing message
        var statusMessage = "Processing your request...";
        _ = statusMessage;
    }

    // ACS0001: Hardcoded notification text
    public string GetNotificationText()
    {
        return "Your session will expire in 5 minutes";
    }
}
