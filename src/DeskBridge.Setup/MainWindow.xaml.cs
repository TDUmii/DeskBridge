using System.Windows;
using System.ComponentModel;

namespace DeskBridge.Setup;

public partial class MainWindow : Window
{
    private readonly InstallerService _installer = new();
    private bool _finished;
    private bool _installing;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_finished)
        {
            Close();
            return;
        }

        InstallButton.IsEnabled = false;
        _installing = true;
        FeatureList.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        Heading.Text = "Installing DeskBridge";
        Description.Text = "Files stay on this PC while the installer prepares the local app and protected browser connection.";

        var progress = new Progress<InstallProgress>(update =>
        {
            ProgressText.Text = update.Message;
            InstallProgressBar.Value = update.Percentage;
        });

        try
        {
            InstallResult result = await _installer.InstallAsync(progress, CancellationToken.None);
            var convenienceWarnings = new List<string>();
            try
            {
                Clipboard.SetText(result.ExtensionPath);
            }
            catch
            {
                convenienceWarnings.Add("Copy the extension path shown below manually.");
            }
            convenienceWarnings.AddRange(_installer.OpenInstalledApplications(result.ExtensionPath));
            Heading.Text = "DeskBridge is ready";
            Description.Text = $"The app is installed. In Chrome, enable Developer mode, choose Load unpacked, then paste the extension path shown below. It is already copied when Windows allows clipboard access:\n\n{result.ExtensionPath}";
            ProgressText.Text = convenienceWarnings.Count == 0
                ? "Installed successfully. Complete the one Chrome confirmation."
                : $"Installed successfully. {string.Join(" ", convenienceWarnings)}";
            InstallProgressBar.Value = 100;
            FooterText.Text = "Chrome security requires the final extension confirmation.";
        }
        catch (Exception exception)
        {
            Heading.Text = "Installation needs attention";
            Description.Text = $"{exception.Message}\n\nA previous installed version was restored when possible. Run this installer again. If the problem repeats, restart Windows and retry.";
            ProgressText.Text = "No workspace files or accepted results were removed.";
            InstallProgressBar.Value = 0;
            FooterText.Text = "Close the installer, then run it again.";
        }

        _installing = false;
        _finished = true;
        InstallButton.Content = "Close installer";
        InstallButton.IsEnabled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_installing)
        {
            e.Cancel = true;
            FooterText.Text = "Installation is still running. This window will unlock when it is safe to close.";
        }
        base.OnClosing(e);
    }
}
