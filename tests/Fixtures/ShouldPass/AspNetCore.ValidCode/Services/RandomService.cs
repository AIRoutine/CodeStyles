namespace AspNetCore.ValidCode.Services;

public interface IRandomService
{
    int GetRandomNumber(int min, int max);
    string GenerateId();
}

public sealed class RandomService : IRandomService
{
    // GOOD: Random is acceptable for non-security scenarios in ASP.NET Core (CA5394 is relaxed)
    private readonly Random _random = new();

    public int GetRandomNumber(int min, int max) => _random.Next(min, max);

    public string GenerateId()
    {
        // GOOD: Using Random for non-cryptographic ID generation
        var bytes = new byte[8];
        _random.NextBytes(bytes);
        return Convert.ToHexString(bytes);
    }
}
