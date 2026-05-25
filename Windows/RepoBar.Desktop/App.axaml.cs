using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RepoBar.Desktop.Platform;
using RepoBar.Desktop.ViewModels;
using RepoBar.Desktop.Views;

namespace RepoBar.Desktop;

public sealed partial class App : Application, IDisposable
{
    private AvaloniaTrayIconAdapter? trayIconAdapter;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            RepoBarWindowService windowService = new(() => desktop.MainWindow);
            ShellViewModel viewModel = ShellViewModel
                .CreateDefaultAsync(windowService)
                .GetAwaiter()
                .GetResult();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            DataContext = viewModel;
            InitializeTray(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeTray(ShellViewModel viewModel)
    {
        trayIconAdapter = new AvaloniaTrayIconAdapter();
        trayIconAdapter.Initialize(new TrayIconSpec(
            "avares://RepoBar.Desktop/Assets/RepoBar.ico",
            "RepoBar",
            viewModel.ShowDashboardCommand,
            [
                new TrayMenuItemSpec("Account", viewModel.ShowSettingsCommand),
                new TrayMenuItemSpec("Refresh", viewModel.RefreshCommand),
                new TrayMenuItemSpec("Settings", viewModel.ShowSettingsCommand),
                new TrayMenuItemSpec("Updates and Help", viewModel.ShowSettingsCommand),
                new TrayMenuItemSpec("Quit", viewModel.QuitCommand),
            ]));
    }

    public void Dispose()
    {
        trayIconAdapter?.Dispose();
    }
}
