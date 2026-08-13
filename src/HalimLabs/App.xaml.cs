using System.IO;
using System.Windows;
using System.Windows.Threading;
using HalimLabs.Configuration;
using HalimLabs.Services.Abstractions;
using HalimLabs.Services.Image;
using HalimLabs.Services.Persistence;
using HalimLabs.Services.Support;
using HalimLabs.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HalimLabs;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, _) => { };
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((_, services) =>
            {
                services.Configure<SupportOptions>(options =>
                {
                    options.DeveloperName = SupportConstants.DeveloperName;
                    options.FooterText = SupportConstants.FooterText;
                    options.SupportText = SupportConstants.SupportText;
                    options.UsdtAddress = SupportConstants.UsdtAddress;
                    options.KofiUrl = SupportConstants.KofiUrl;
                    options.Iban = SupportConstants.Iban;
                    options.IbanHolder = SupportConstants.IbanHolder;
                    options.BankName = SupportConstants.BankName;
                });

                services.AddHttpClient("ImageApi", client =>
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                });

                services.AddHttpClient("TranslateApi", client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.TryAddWithoutValidation(
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) HalimLabsImage/1.0");
                });

                services.AddSingleton<ISupportInfoProvider, SupportInfoProvider>();
                services.AddSingleton<IImageSettingsRepository, ImageSettingsRepository>();
                services.AddSingleton<IImageGenerationService, NvidiaImageGenerationService>();
                services.AddSingleton<IPromptTranslationService, NvidiaPromptTranslationService>();
                services.AddSingleton<IImageCaptionService, NvidiaImageCaptionService>();

                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<HelpViewModel>();
                services.AddTransient<Func<SettingsViewModel>>(sp => () => sp.GetRequiredService<SettingsViewModel>());
                services.AddTransient<Func<HelpViewModel>>(sp => () => sp.GetRequiredService<HelpViewModel>());
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await _host.StartAsync().ConfigureAwait(true);
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            MessageBox.Show($"Unexpected error:\n{e.Exception.Message}", "Halim Labs 3",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
            // ignore
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
