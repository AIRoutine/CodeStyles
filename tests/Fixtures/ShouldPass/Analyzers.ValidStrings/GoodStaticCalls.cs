using System;
using System.IO;
using System.Linq;

namespace Analyzers.ValidStrings;

/// <summary>
/// This file contains only allowed static calls (System/Microsoft namespaces).
/// </summary>
public class StaticCallTestService
{
    // OK: System.IO.Path
    public string CombinePaths(string a, string b)
    {
        return Path.Combine(a, b);
    }

    // OK: Console (System namespace)
    public void LogMessage(string message)
    {
        Console.WriteLine(message);
    }

    // OK: System.Math
    public double Calculate(double x, double y)
    {
        return Math.Max(x, y);
    }

    // OK: System.Linq.Enumerable
    public int[] GetSorted(int[] values)
    {
        return values.OrderBy(x => x).ToArray();
    }

    // OK: System.Convert
    public int ParseNumber(string input)
    {
        return Convert.ToInt32(input);
    }

    // OK: System.Guid
    public string GetNewId()
    {
        return Guid.NewGuid().ToString();
    }

    // OK: System.DateTime
    public DateTime GetCurrentTime()
    {
        return DateTime.UtcNow;
    }

    // OK: System.Environment
    public string GetMachineName()
    {
        return Environment.MachineName;
    }

    // OK: String static methods
    public bool IsEmpty(string value)
    {
        return string.IsNullOrEmpty(value);
    }

    // OK: File static methods
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }
}
