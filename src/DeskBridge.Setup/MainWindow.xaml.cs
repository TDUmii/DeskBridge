using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;

namespace DeskBridge.Setup;

public partial class MainWindow : Window
{
    private readonly InstallerService _installer = new();
    private bool _finished;
    private bool _installing;
    private bool _vietnamese;

    public MainWindow()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    private void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || LanguagePicker?.SelectedItem is not ComboBoxItem item) return;
        _vietnamese = Equals(item.Content, "Tiếng Việt");
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Title = _vietnamese ? "Cài đặt DeskBridge" : "Install DeskBridge";
        Subtitle.Text = L("Verified ChatGPT Web file agent", "Trợ lý tệp ChatGPT Web có kiểm tra cục bộ");
        if (!_installing && !_finished)
        {
            Heading.Text = L("One install. Everything in its place.", "Một lần cài đặt. Mọi thứ đúng vị trí.");
            Description.Text = L("Installs the Windows app, local verifier, Chrome native connection, shortcuts, and extension files for this account. No API key or administrator access required.", "Cài ứng dụng Windows, bộ kiểm tra cục bộ, kết nối Chrome native, lối tắt và tệp tiện ích cho tài khoản này. Không cần API key hoặc quyền quản trị.");
            FooterText.Text = L("Installs to your local Windows profile", "Cài vào tài khoản Windows hiện tại");
            InstallButton.Content = L("Install DeskBridge", "Cài DeskBridge");
        }
        FeatureApp.Text = L("App and local verifier", "Ứng dụng và bộ kiểm tra cục bộ");
        FeatureNative.Text = L("Protected Chrome Native Messaging", "Chrome Native Messaging được bảo vệ");
        FeatureShortcuts.Text = L("Start menu and desktop shortcuts", "Lối tắt trong Start Menu và Desktop");
        FeatureExtension.Text = L("Extension folder prepared and copied", "Đã chuẩn bị và sao chép thư mục tiện ích");
        PrivacyLabel.Text = L("Privacy boundary", "Giới hạn riêng tư");
        PrivacyTitle.Text = L("ChatGPT Web only", "Chỉ dùng ChatGPT Web");
        PrivacyDescription.Text = L("No Codex, API key, cookies, or browser tokens. Create-new sends no workspace files or local paths.", "Không dùng Codex, API key, cookie hoặc token trình duyệt. Chế độ tạo mới không gửi tệp hay đường dẫn cục bộ.");
        ChromeNotice.Text = L("Chrome protects extension installation. After this installer finishes, Chrome opens the one required confirmation step. The extension path is already copied.", "Chrome bảo vệ việc cài tiện ích. Sau khi cài xong, Chrome sẽ mở bước xác nhận bắt buộc duy nhất. Đường dẫn tiện ích đã được sao chép.");
        SigningNotice.Text = L("Until this installer is code-signed, Windows may also show an Unknown publisher or SmartScreen confirmation.", "Do bộ cài chưa được ký mã, Windows có thể hiện cảnh báo Unknown publisher hoặc SmartScreen.");
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
        Heading.Text = L("Installing DeskBridge", "Đang cài DeskBridge");
        Description.Text = L("Files stay on this PC while the installer prepares the local app and protected browser connection.", "Tệp vẫn nằm trên máy này trong khi bộ cài chuẩn bị ứng dụng cục bộ và kết nối trình duyệt được bảo vệ.");

        var progress = new Progress<InstallProgress>(update =>
        {
            ProgressText.Text = LocalizeProgress(update.Message);
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
                convenienceWarnings.Add(L("Copy the extension path shown below manually.", "Hãy sao chép thủ công đường dẫn tiện ích bên dưới."));
            }
            convenienceWarnings.AddRange(_installer.OpenInstalledApplications(result.ExtensionPath));
            Heading.Text = L("DeskBridge is ready", "DeskBridge đã sẵn sàng");
            Description.Text = $"{L("The app is installed. In Chrome, enable Developer mode, choose Load unpacked, then paste the extension path shown below. It is already copied when Windows allows clipboard access:", "Ứng dụng đã được cài. Trong Chrome, bật Chế độ dành cho nhà phát triển, chọn Tải tiện ích đã giải nén rồi dán đường dẫn bên dưới. Đường dẫn đã được sao chép nếu Windows cho phép:")}\n\n{result.ExtensionPath}";
            ProgressText.Text = convenienceWarnings.Count == 0
                ? L("Installed successfully. Complete the one Chrome confirmation.", "Cài đặt thành công. Hãy hoàn tất một bước xác nhận trong Chrome.")
                : $"{L("Installed successfully.", "Cài đặt thành công.")} {string.Join(" ", convenienceWarnings)}";
            InstallProgressBar.Value = 100;
            FooterText.Text = L("Chrome security requires the final extension confirmation.", "Bảo mật Chrome yêu cầu xác nhận tiện ích lần cuối.");
        }
        catch (Exception exception)
        {
            Heading.Text = L("Installation needs attention", "Cần kiểm tra quá trình cài đặt");
            Description.Text = $"{exception.Message}\n\n{L("A previous installed version was restored when possible. Run this installer again. If the problem repeats, restart Windows and retry.", "Bản đã cài trước đó được khôi phục khi có thể. Hãy chạy lại bộ cài. Nếu lỗi lặp lại, khởi động lại Windows rồi thử lại.")}";
            ProgressText.Text = L("No workspace files or accepted results were removed.", "Không xóa tệp trong không gian làm việc hoặc kết quả đã chấp nhận.");
            InstallProgressBar.Value = 0;
            FooterText.Text = L("Close the installer, then run it again.", "Đóng bộ cài rồi chạy lại.");
        }

        _installing = false;
        _finished = true;
        InstallButton.Content = L("Close installer", "Đóng bộ cài");
        InstallButton.IsEnabled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_installing)
        {
            e.Cancel = true;
            FooterText.Text = L("Installation is still running. This window will unlock when it is safe to close.", "Quá trình cài đặt vẫn đang chạy. Cửa sổ sẽ cho phép đóng khi an toàn.");
        }
        base.OnClosing(e);
    }

    private string L(string english, string vietnamese) => _vietnamese ? vietnamese : english;
    private string LocalizeProgress(string message) => message switch
    {
        "Preparing verified application files..." => L(message, "Đang chuẩn bị tệp ứng dụng đã kiểm tra..."),
        "Installing DeskBridge for this Windows account..." => L(message, "Đang cài DeskBridge cho tài khoản Windows này..."),
        "Registering the protected Chrome connection..." => L(message, "Đang đăng ký kết nối Chrome được bảo vệ..."),
        "Finalizing the installed application..." => L(message, "Đang hoàn tất ứng dụng..."),
        "DeskBridge is installed." => L(message, "DeskBridge đã được cài đặt."),
        _ => message
    };
}
