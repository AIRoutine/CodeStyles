namespace Common.BadNaming;

// BAD: Interface without I prefix
public interface Service
{
    void Execute();
}

// BAD: Interface without I prefix
public interface Repository
{
    int Count();
}

public sealed class BadService : Service
{
    // BAD: Private field without _ prefix
    private readonly int count;

    // BAD: Static field without s_ prefix
    private static string defaultName = "test";

    public BadService()
    {
        count = 0;
    }

    public void Execute()
    {
        Console.WriteLine(defaultName + count);
    }

    // BAD: Async method without Async suffix
    public async Task<string> GetData()
    {
        await Task.Delay(1);
        return "data";
    }

    // BAD: Async method without Async suffix
    public async Task Process()
    {
        await Task.Delay(1);
    }
}

// BAD: Type parameter without T prefix
public interface GenericService<Entity>
{
    Entity? Find(int id);
}
