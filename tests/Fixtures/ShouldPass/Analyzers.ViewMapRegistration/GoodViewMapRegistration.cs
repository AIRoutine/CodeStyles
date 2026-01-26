using Microsoft.Extensions.DependencyInjection;

namespace Analyzers.ViewMapRegistration;

/// <summary>
/// Interfaces and services (not ViewModels) for testing.
/// </summary>
public interface IMyService
{
    void DoWork();
}

public class MyService : IMyService
{
    public void DoWork() { }
}

public interface IDataRepository
{
    Task<string> GetDataAsync();
}

public class DataRepository : IDataRepository
{
    public Task<string> GetDataAsync() => Task.FromResult("data");
}

public class NotificationHandler
{
    public void Handle() { }
}

/// <summary>
/// This file contains only valid DI registrations (no ViewModels).
/// None of these should trigger ACS0020.
/// </summary>
public static class GoodViewMapRegistration
{
    // OK: Registering a service with interface
    public static IServiceCollection RegisterServices(IServiceCollection services)
    {
        services.AddTransient<IMyService, MyService>();
        return services;
    }

    // OK: Registering a repository with interface
    public static IServiceCollection RegisterRepository(IServiceCollection services)
    {
        services.AddSingleton<IDataRepository, DataRepository>();
        return services;
    }

    // OK: Registering a handler (not a ViewModel)
    public static IServiceCollection RegisterHandler(IServiceCollection services)
    {
        services.AddTransient<NotificationHandler>();
        return services;
    }

    // OK: Registering with typeof (not a ViewModel)
    public static IServiceCollection RegisterWithTypeOf(IServiceCollection services)
    {
        services.AddTransient(typeof(NotificationHandler));
        return services;
    }

    // OK: Registering with factory (not a ViewModel)
    public static IServiceCollection RegisterWithFactory(IServiceCollection services)
    {
        services.AddSingleton<IMyService>(sp => new MyService());
        return services;
    }
}
