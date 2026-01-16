namespace Common.StaticCalls;

// This file contains forbidden static method calls that should fail the build

public interface ICalculationService
{
    int Calculate(int a, int b);
}

public interface IFormatService
{
    string Format(string input);
}

// BAD: Using custom static helper classes instead of DI
public static class MyHelper
{
    public static int Calculate(int a, int b) => a + b;
}

public static class StringUtils
{
    public static string Format(string input) => input.ToUpper();
}

public static class ThirdPartyLib
{
    public static class StaticClass
    {
        public static void DoSomething() { }
    }
}

public sealed class BadService
{
    // BAD: Direct static call to custom helper
    public int CalculateSum(int a, int b)
    {
        return MyHelper.Calculate(a, b);  // Should fail: custom static call
    }

    // BAD: Direct static call to custom utility
    public string FormatText(string input)
    {
        return StringUtils.Format(input);  // Should fail: custom static call
    }

    // BAD: Nested static class call
    public void DoWork()
    {
        ThirdPartyLib.StaticClass.DoSomething();  // Should fail: third-party static call
    }

    // GOOD: These should be allowed - System namespace
    public void AllowedCalls()
    {
        var now = DateTime.Now;
        var exists = File.Exists("test.txt");
        var max = Math.Max(1, 2);
        var empty = string.IsNullOrEmpty("");
        var result = Task.FromResult(42);
        Console.WriteLine("test");
    }
}
