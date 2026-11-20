using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Infrastructure.Persistence;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContextFactory<EquipmentBorrowingDbContext>(options =>
                options.UseNpgsql(DatabasePaths.TryGetConnectionString(configuration) ?? string.Empty));

            services.AddSingleton<IStudentRepository, EfStudentRepository>();
            services.AddSingleton<IEquipmentRepository, EfEquipmentRepository>();
            services.AddSingleton<IBorrowingRepository, EfBorrowingRepository>();
            services.AddSingleton<DatabaseInitializer>();

            services.AddTransient<BorrowEquipmentService>();
            services.AddTransient<ReturnEquipmentService>();
            services.AddTransient<GetAllEquipmentService>();
            services.AddTransient<GetAvailableEquipmentService>();
            services.AddTransient<GetAllStudentsService>();
            services.AddTransient<GetActiveBorrowingsService>();
            services.AddTransient<ManageStudentsService>();
            services.AddTransient<ManageEquipmentService>();
            services.AddTransient<BrowseBorrowingsService>();
            services.AddSingleton<MainViewModel>();

            _serviceProvider = services.BuildServiceProvider();
            var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var window = new Views.MainWindow { DataContext = viewModel };
            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => (_serviceProvider as IDisposable)?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
