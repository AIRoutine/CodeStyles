namespace Analyzers.HardcodedStrings;

// Helper class to test static call detection
public static class MyHelper
{
    public static string Calculate() => "result";
    public static int Process(int value) => value * 2;
}

public static class StringUtils
{
    public static string Format(string input) => input.ToUpper();
}

public static class ThirdPartyLib
{
    public static class Nested
    {
        public static void DoSomething() { }
    }
}

/// <summary>
/// This file contains static calls that should trigger ACS0002.
/// Note: This is in a *Service.cs-like context for testing purposes.
/// </summary>
public class StaticCallTestService
{
    // ACS0002: Static call to custom type
    public string DoWork()
    {
        return MyHelper.Calculate();
    }

    // ACS0002: Static call to custom utility
    public string FormatText(string input)
    {
        return StringUtils.Format(input);
    }

    // ACS0002: Static call with parameter
    public int ProcessValue(int value)
    {
        return MyHelper.Process(value);
    }

    // OK: System namespace calls are allowed
    public string GetPath()
    {
        return System.IO.Path.Combine("a", "b");
    }

    // OK: Console is System namespace
    public void Log(string message)
    {
        System.Console.WriteLine(message);
    }

    // ACS0002: Nested static class call
    public void DoNestedWork()
    {
        ThirdPartyLib.Nested.DoSomething();
    }
}
