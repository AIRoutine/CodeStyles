namespace Common.ValidCode;

public interface IDataService
{
    public Task<string> GetDataAsync();
}

public interface IRepository
{
    public Task<int> CountAsync();
}

public sealed class ValidService(IRepository repository) : IDataService
{
    public async Task<string> GetDataAsync()
    {
        var count = await repository.CountAsync().ConfigureAwait(false);
        return count > 0 ? "data" : string.Empty;
    }
}

public sealed class InMemoryRepository : IRepository
{
    private readonly List<int> _items = [];

    public Task<int> CountAsync() => Task.FromResult(_items.Count);

    public void Add(int item) => _items.Add(item);
}
