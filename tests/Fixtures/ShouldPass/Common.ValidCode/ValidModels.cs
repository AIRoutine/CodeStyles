namespace Common.ValidCode;

public sealed record Person(string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed class Calculator(int precision = 2)
{
    public double Add(double a, double b) => Math.Round(a + b, precision);

    public double Subtract(double a, double b) => Math.Round(a - b, precision);
}

public static class StringExtensions
{
    public static string ToTitleCase(this string input) =>
        string.IsNullOrEmpty(input)
            ? input
            : char.ToUpperInvariant(input[0]) + input[1..].ToUpperInvariant();
}

public interface IGenericService<TEntity, TKey>
    where TEntity : class
    where TKey : struct
{
    public Task<TEntity?> FindByIdAsync(TKey id);
    public Task SaveAsync(TEntity entity);
}
