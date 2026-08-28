using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DeskBridge.App.Models;
using DeskBridge.App.Services;
using DeskBridge.App.ViewModels;
using Microsoft.Win32;

namespace DeskBridge.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly CancellationTokenSource _permissionCancellation = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            _ = new PermissionBroker().RunAsync(_permissionCancellation.Token);
        };
        Closed += (_, _) => _permissionCancellation.Cancel();
    }

    private async void ChooseWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose the allowed DeskBridge workspace", Multiselect = false };
        if (dialog.ShowDialog(this) == true) await _viewModel.SetWorkspaceAsync(dialog.FolderName);
    }

    private void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasWorkspace) return;
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { _viewModel.Workspace } });
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await _viewModel.RefreshAsync();
    private async void Permission_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: PermissionRow row } && IsLoaded) await _viewModel.SavePermissionAsync(row);
    }
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => MainViewModel.OpenLogs();
    private void ClearLogs_Click(object sender, RoutedEventArgs e) => _viewModel.ClearLogs();
}
