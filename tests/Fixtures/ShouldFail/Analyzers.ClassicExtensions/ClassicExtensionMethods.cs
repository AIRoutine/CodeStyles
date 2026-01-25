namespace Analyzers.ClassicExtensions;

/// <summary>
/// This file contains classic extension methods using the 'this' parameter syntax.
/// These should trigger ACS0018 warnings recommending C# 14 extension block syntax.
/// </summary>
public static class ClassicExtensionMethods
{
    // BAD: Classic extension method with 'this' parameter
    public static int WordCount(this string str)
    {
        return str.Split(' ').Length;
    }

    // BAD: Classic extension method with 'this' parameter and additional parameters
    public static string Truncate(this string str, int maxLength)
    {
        if (str.Length <= maxLength)
            return str;
        return str.Substring(0, maxLength) + "...";
    }

    // BAD: Generic classic extension method
    public static bool IsEmpty<T>(this IEnumerable<T> source)
    {
        return !source.Any();
    }

    // BAD: Classic extension method with ref this parameter
    public static void Increment(this ref int number)
    {
        number++;
    }
}

/// <summary>
/// Another static class with classic extension methods.
/// </summary>
public static class MoreClassicExtensions
{
    // BAD: Extension method on DateTime
    public static bool IsWeekend(this DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    // BAD: Extension method with nullable receiver
    public static string SafeToString(this object? obj)
    {
        return obj?.ToString() ?? string.Empty;
    }
}
