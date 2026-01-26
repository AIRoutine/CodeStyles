using Microsoft.Extensions.DependencyInjection;

namespace Analyzers.ViewModelRegistration;

/// <summary>
/// Dummy ViewModels for testing.
/// </summary>
public class MainViewModel { }
public class DetailViewModel { }
public class SettingsViewModel { }

/// <summary>
/// This file contains forbidden manual ViewModel DI registrations.
/// ViewModels should be registered via Uno Extensions ViewMap instead.
/// These should trigger ACS0020 errors.
/// </summary>
public static class BadViewModelRegistration
{
    // BAD: AddTransient with generic ViewModel type
    public static IServiceCollection RegisterTransient(IServiceCollection services)
    {
        services.AddTransient<MainViewModel>();
        return services;
    }

    // BAD: AddScoped with generic ViewModel type
    public static IServiceCollection RegisterScoped(IServiceCollection services)
    {
        services.AddScoped<DetailViewModel>();
        return services;
    }

    // BAD: AddSingleton with generic ViewModel type
    public static IServiceCollection RegisterSingleton(IServiceCollection services)
    {
        services.AddSingleton<SettingsViewModel>();
        return services;
    }

    // BAD: AddTransient with typeof() argument
    public static IServiceCollection RegisterWithTypeOf(IServiceCollection services)
    {
        services.AddTransient(typeof(MainViewModel));
        return services;
    }

    // BAD: AddSingleton with factory delegate
    public static IServiceCollection RegisterWithFactory(IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>(sp => new MainViewModel());
        return services;
    }
}
