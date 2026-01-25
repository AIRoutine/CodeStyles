namespace Analyzers.ModernExtensions;

/// <summary>
/// This file contains extension members using the new C# 14 extension block syntax.
/// These should NOT trigger any ACS0018 warnings.
/// </summary>
public static class ModernExtensionMethods
{
    // GOOD: Modern extension block syntax for instance extensions
    extension(string str)
    {
        public int WordCount()
        {
            return str.Split(' ').Length;
        }

        public string Truncate(int maxLength)
        {
            if (str.Length <= maxLength)
                return str;
            return str.Substring(0, maxLength) + "...";
        }
    }

    // GOOD: Generic extension block
    extension<T>(IEnumerable<T> source)
    {
        public bool IsEmpty()
        {
            return !source.Any();
        }
    }

    // GOOD: Extension property (only possible with new syntax!)
    extension(DateTime date)
    {
        public bool IsWeekend => date.DayOfWeek == DayOfWeek.Saturday
            || date.DayOfWeek == DayOfWeek.Sunday;
    }

    // GOOD: Static extension (extends the type itself)
    extension(int)
    {
        public static int MaxValue => int.MaxValue;
    }
}

/// <summary>
/// Regular static methods that should not trigger the analyzer.
/// </summary>
public static class RegularStaticMethods
{
    // GOOD: Regular static method (not an extension method)
    public static int Add(int a, int b)
    {
        return a + b;
    }

    // GOOD: Static method with any parameter, but no 'this' modifier
    public static string Format(string template, params object[] args)
    {
        return string.Format(template, args);
    }
}
