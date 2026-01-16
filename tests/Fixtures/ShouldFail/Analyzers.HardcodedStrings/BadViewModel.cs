namespace Analyzers.HardcodedStrings;

/// <summary>
/// This ViewModel contains hardcoded strings that should trigger ACS0001.
/// </summary>
public class BadViewModel
{
    // ACS0001: Hardcoded UI text
    public string Title { get; set; } = "Welcome to the app";

    // ACS0001: Hardcoded error message
    public string GetErrorMessage()
    {
        return "Something went wrong. Please try again.";
    }

    // ACS0001: Hardcoded validation message
    public string ValidateInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "Input cannot be empty";

        return "Input is valid";
    }

    // ACS0001: Hardcoded button text
    public string SubmitButtonText => "Submit";

    // ACS0001: Hardcoded dialog content
    public void ShowDialog()
    {
        var message = "Are you sure you want to continue?";
        var title = "Confirmation";
        // Use message and title...
        _ = message;
        _ = title;
    }
}
