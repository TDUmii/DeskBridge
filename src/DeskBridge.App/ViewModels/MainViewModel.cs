using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using DeskBridge.App.Models;
using DeskBridge.App.Services;
using DeskBridge.Core.Agent;
using DeskBridge.Core.Models;
using DeskBridge.Core.Security;
using DeskBridge.Core.Services;
using DeskBridge.Core.Skills;

namespace DeskBridge.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ActivityLogger _activityLogger = new();
    private readonly BrowserAgentStore _browserAgentStore = new();
    private DeskBridgeSettings _settings = new();
    private string _workspace = "No workspace selected";
    private string _selectedTheme = "System";
    private string _selectedLanguage = "English";
    private AgentRunMode _agentMode = AgentRunMode.CreateNew;
    private string _agentSourcePath = string.Empty;
    private string _agentRequest = string.Empty;
    private int _agentIterations = 4;
    private string _agentStatus = "Ready to create";
    private string _agentStatusDetail = "Choose a workspace and describe the new result you want. No workspace files will be uploaded.";
    private bool _isAgentRunning;
    private string? _agentResultPath;
    private string? _agentRunDirectory;
    private CancellationTokenSource? _agentCancellation;

    public string Workspace { get => _workspace; private set { _workspace = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWorkspace)); OnPropertyChanged(nameof(CanStartAgent)); OnPropertyChanged(nameof(CanChooseAgentSource)); } }
    public bool HasWorkspace => !string.IsNullOrWhiteSpace(_settings.WorkspacePath) && Directory.Exists(_settings.WorkspacePath);
    public ObservableCollection<ActivityEntry> Activities { get; } = [];
    public ObservableCollection<AssetRow> Assets { get; } = [];
    public ObservableCollection<PermissionRow> Permissions { get; } = [];
    public ObservableCollection<SkillIntegrationRow> SkillIntegrations { get; } = [];
    public ObservableCollection<AgentStepRow> AgentSteps { get; } = [];
    public ObservableCollection<string> Policies { get; } = [];
    public ObservableCollection<string> ThemeModes { get; } = [];
    public string[] LanguageModes { get; } = ["English", "Tiếng Việt"];
    public int[] AgentIterationOptions { get; } = [2, 3, 4, 5, 6, 8];
    public string SelectedTheme { get => _selectedTheme; set { _selectedTheme = value; OnPropertyChanged(); } }
    public string SelectedLanguage { get => _selectedLanguage; set { _selectedLanguage = value; OnPropertyChanged(); } }
    public bool IsCreateNewMode { get => _agentMode == AgentRunMode.CreateNew; set { if (value) SetAgentMode(AgentRunMode.CreateNew); } }
    public bool IsWorkspaceContextMode { get => _agentMode == AgentRunMode.WorkspaceContext; set { if (value) SetAgentMode(AgentRunMode.WorkspaceContext); } }
    public bool IsImproveFileMode { get => _agentMode == AgentRunMode.ImproveFile; set { if (value) SetAgentMode(AgentRunMode.ImproveFile); } }
    public bool RequiresAgentSource => _agentMode == AgentRunMode.ImproveFile;
    public string AgentSourcePath { get => _agentSourcePath; set { _agentSourcePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartAgent)); } }
    public string AgentRequest { get => _agentRequest; set { _agentRequest = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartAgent)); } }
    public string AgentModel => "GPT-5.6 Sol";
    public string AgentReasoning => LocalizationService.Vietnamese ? "Cao · 3/3" : "High · 3/3";
    public string AgentTransport => LocalizationService.Vietnamese ? "Chỉ ChatGPT Web · không dùng Codex hoặc API" : "ChatGPT Web only · never Codex or API";
    public int AgentIterations { get => _agentIterations; set { _agentIterations = value; OnPropertyChanged(); } }
    public string AgentStatus { get => _agentStatus; private set { _agentStatus = value; OnPropertyChanged(); } }
    public string AgentStatusDetail { get => _agentStatusDetail; private set { _agentStatusDetail = value; OnPropertyChanged(); } }
    public string AgentStartLabel => LocalizationService.Vietnamese
        ? (RequiresAgentSource ? "Cải thiện bằng ChatGPT Web" : IsWorkspaceContextMode ? "Tạo từ workspace bằng ChatGPT Web" : "Tạo bằng ChatGPT Web")
        : (RequiresAgentSource ? "Improve in ChatGPT Web" : IsWorkspaceContextMode ? "Build from workspace in ChatGPT Web" : "Create in ChatGPT Web");
    public string AgentRequirementsText => IsWorkspaceContextMode
        ? (LocalizationService.Vietnamese ? "ChatGPT chỉ được đọc các đoạn ngữ cảnh cần thiết. Tệp nhạy cảm, thư mục build, thao tác ghi, lệnh và Codex đều bị chặn." : "ChatGPT may read only necessary context snippets. Sensitive files, build folders, writes, commands, and Codex are blocked.")
        : RequiresAgentSource
        ? (LocalizationService.Vietnamese ? "Cần Chrome, tiện ích DeskBridge, tài khoản ChatGPT Web đã đăng nhập, không gian làm việc, tệp nguồn và yêu cầu rõ ràng." : "Requires Chrome, the DeskBridge extension, a signed-in ChatGPT Web account, a workspace, a source file, and a clear request.")
        : (LocalizationService.Vietnamese ? "Cần Chrome, tiện ích DeskBridge, tài khoản ChatGPT Web đã đăng nhập, không gian làm việc và yêu cầu rõ ràng. Không tải tệp trong không gian làm việc lên." : "Requires Chrome, the DeskBridge extension, a signed-in ChatGPT Web account, a workspace, and a clear request. No workspace files are uploaded.");
    public bool IsAgentRunning { get => _isAgentRunning; private set { _isAgentRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartAgent)); OnPropertyChanged(nameof(CanChangeAgentMode)); OnPropertyChanged(nameof(CanChooseAgentSource)); } }
    public bool CanChangeAgentMode => !IsAgentRunning;
    public bool CanChooseAgentSource => HasWorkspace && !IsAgentRunning;
    public bool CanStartAgent => HasWorkspace && !IsAgentRunning && !string.IsNullOrWhiteSpace(AgentRequest) &&
        (!RequiresAgentSource || HasValidAgentSource());
    public bool HasAgentResult => !string.IsNullOrWhiteSpace(_agentResultPath) && File.Exists(_agentResultPath);
    public bool HasAgentRun => !string.IsNullOrWhiteSpace(_agentRunDirectory) && Directory.Exists(_agentRunDirectory);
    public bool IsInitialized { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        SelectedLanguage = _settings.LanguageMode.Equals("vi", StringComparison.OrdinalIgnoreCase) ? "Tiếng Việt" : "English";
        BuildLocalizedOptions();
        Workspace = string.IsNullOrWhiteSpace(_settings.WorkspacePath) ? Localize("No workspace selected", "Chưa chọn không gian làm việc") : _settings.WorkspacePath;
        SelectedTheme = ToThemeDisplay(_settings.ThemeMode);
        AgentIterations = _settings.Agent.MaximumIterations;
        BuildPermissions();
        BuildSkillIntegrations();
        await RefreshAsync();
        IsInitialized = true;
    }

    public async Task SetWorkspaceAsync(string path)
    {
        _settings = _settings with { WorkspacePath = Path.GetFullPath(path), WorkspaceMode = true };
        await _settingsStore.SaveAsync(_settings);
        AgentSourcePath = string.Empty;
        Workspace = _settings.WorkspacePath;
        OnPropertyChanged(nameof(CanStartAgent));
        await RefreshAssetsAsync();
    }

    public async Task<AgentRunResult?> StartAgentAsync()
    {
        if (!CanStartAgent) return null;
        await SaveAgentSettingsAsync();
        _agentCancellation = new CancellationTokenSource();
        IsAgentRunning = true;
        _agentResultPath = null;
        _agentRunDirectory = null;
        OnPropertyChanged(nameof(HasAgentResult));
        OnPropertyChanged(nameof(HasAgentRun));
        AgentSteps.Clear();
        AgentStatus = Localize("Starting agent", "Đang bắt đầu tác vụ");
        AgentStatusDetail = Localize("Opening a dedicated ChatGPT Web tab and waiting for the DeskBridge extension.", "Đang mở tab ChatGPT Web riêng và chờ tiện ích DeskBridge.");
        var progress = new Progress<AgentProgress>(item =>
        {
            AgentStatus = LocalizeAgentText(item.Stage);
            AgentStatusDetail = LocalizeAgentText(item.Message);
            AgentSteps.Insert(0, new AgentStepRow(DateTime.Now.ToString("HH:mm:ss"), LocalizeAgentText(item.Stage), LocalizeAgentText(item.Message), item.Detail));
        });

        try
        {
            var service = new AgentRunService(_browserAgentStore);
            var options = new AgentRunOptions(AgentIterations);
            var source = RequiresAgentSource ? AgentSourcePath : null;
            var result = await service.RunAsync(new AgentRunRequest(_settings.WorkspacePath!, source, AgentRequest, options, _agentMode), progress, _agentCancellation.Token);
            _agentResultPath = result.BestArtifactPath;
            _agentRunDirectory = result.RunDirectory;
            AgentStatus = result.Status switch
            {
                "completed" => Localize("Finished", "Hoàn tất"),
                "cancelled" => Localize("Cancelled", "Đã hủy"),
                "failed" => Localize("Needs attention", "Cần kiểm tra"),
                _ => Localize("Best candidate preserved", "Đã giữ lại bản tốt nhất")
            };
            AgentStatusDetail = LocalizeAgentText(result.Summary);
            OnPropertyChanged(nameof(HasAgentResult));
            OnPropertyChanged(nameof(HasAgentRun));
            return result;
        }
        catch (OperationCanceledException)
        {
            AgentStatus = Localize("Cancelled", "Đã hủy");
            AgentStatusDetail = RequiresAgentSource
                ? Localize("The current request was stopped. The original file was not changed.", "Yêu cầu hiện tại đã dừng. Tệp gốc không bị thay đổi.")
                : Localize("The current request was stopped. Existing workspace files were not modified; local run evidence was retained.", "Yêu cầu hiện tại đã dừng. Các tệp hiện có không bị thay đổi và bằng chứng tác vụ cục bộ vẫn được giữ lại.");
            return null;
        }
        catch (Exception exception)
        {
            AgentStatus = Localize("Needs attention", "Cần kiểm tra");
            AgentStatusDetail = LocalizeAgentText(exception.Message);
            AgentSteps.Insert(0, new AgentStepRow(DateTime.Now.ToString("HH:mm:ss"), Localize("Error", "Lỗi"), LocalizeAgentText(exception.Message), null));
            return null;
        }
        finally
        {
            IsAgentRunning = false;
            _agentCancellation?.Dispose();
            _agentCancellation = null;
        }
    }

    public void CancelAgent() => _agentCancellation?.Cancel();
    public void OpenAgentResult() => OpenExplorerTarget(_agentResultPath);
    public void OpenAgentRun() => OpenExplorerTarget(_agentRunDirectory);

    public async Task SavePermissionAsync(PermissionRow row)
    {
        var permissions = new Dictionary<string, string>(_settings.Permissions, StringComparer.OrdinalIgnoreCase);
        foreach (var action in row.Actions)
            permissions[action] = ToPolicyId(row.Policy);
        _settings = _settings with { Permissions = permissions };
        await _settingsStore.SaveAsync(_settings);
    }

    public async Task SaveThemeAsync(string displayMode)
    {
        SelectedTheme = displayMode;
        _settings = _settings with { ThemeMode = ToThemeId(displayMode) };
        await _settingsStore.SaveAsync(_settings);
    }

    public async Task SaveLanguageAsync(string displayMode)
    {
        SelectedLanguage = displayMode;
        _settings = _settings with { LanguageMode = displayMode == "Tiếng Việt" ? "vi" : "en" };
        await _settingsStore.SaveAsync(_settings);
    }

    public void ApplyLanguage()
    {
        BuildLocalizedOptions();
        SelectedTheme = ToThemeDisplay(_settings.ThemeMode);
        if (string.IsNullOrWhiteSpace(_settings.WorkspacePath)) Workspace = Localize("No workspace selected", "Chưa chọn không gian làm việc");
        BuildPermissions();
        BuildSkillIntegrations();
        OnPropertyChanged(nameof(AgentReasoning));
        OnPropertyChanged(nameof(AgentTransport));
        OnPropertyChanged(nameof(AgentStartLabel));
        OnPropertyChanged(nameof(AgentRequirementsText));
        if (!IsAgentRunning) SetReadyStatus();
    }

    public async Task SaveSkillIntegrationAsync(SkillIntegrationRow row)
    {
        var integrations = new Dictionary<string, bool>(_settings.SkillIntegrations, StringComparer.OrdinalIgnoreCase)
        {
            [row.Id] = row.Enabled
        };
        _settings = _settings with { SkillIntegrations = integrations };
        await _settingsStore.SaveAsync(_settings);
    }

    private async Task SaveAgentSettingsAsync()
    {
        _settings = _settings with { Agent = _settings.Agent with { MaximumIterations = AgentIterations, Transport = "chatgpt-web-only", RequiredModel = "GPT-5.6 Sol", RequiredReasoning = "High" } };
        await _settingsStore.SaveAsync(_settings);
    }

    public async Task RefreshAsync()
    {
        Activities.Clear();
        foreach (var entry in await _activityLogger.ReadRecentAsync()) Activities.Add(entry);
        await RefreshAssetsAsync();
    }

    public void ClearLogs() { _activityLogger.Clear(); Activities.Clear(); }

    public static void OpenLogs()
    {
        Directory.CreateDirectory(DeskBridgePaths.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { DeskBridgePaths.DataDirectory } });
    }

    private void BuildPermissions()
    {
        Permissions.Clear();
        AddPermission(Localize("Read files", "Đọc tệp"), Localize("Read files and inspect folders in the workspace.", "Đọc tệp và kiểm tra thư mục trong không gian làm việc."), ["read_file", "list_folder"], "allowed");
        AddPermission(Localize("Write files", "Ghi tệp"), Localize("Create, update, and patch workspace content.", "Tạo, cập nhật và vá nội dung trong không gian làm việc."), ["write_file", "create_file", "create_folder", "create_project", "update_project", "patch_file"], "ask");
        AddPermission(Localize("Run commands", "Chạy lệnh"), Localize("Run an allowed program with an argument list.", "Chạy chương trình được phép cùng danh sách đối số."), ["run_command"], "ask");
        AddPermission(Localize("Assets", "Tài nguyên"), Localize("Download, import, resize, compress, and convert images.", "Tải xuống, nhập, đổi kích thước, nén và chuyển đổi ảnh."), ["download_asset", "import_asset", "resize_image", "compress_image", "convert_image"], "ask");
        AddPermission(Localize("Screenshots", "Ảnh chụp màn hình"), Localize("Capture the primary monitor to a local temp file.", "Chụp màn hình chính vào tệp tạm cục bộ."), ["capture_screen"], "ask");
        AddPermission(Localize("Document conversion", "Chuyển đổi tài liệu"), Localize("Convert a supported workspace document to Markdown with the enabled skill adapter.", "Chuyển tài liệu được hỗ trợ sang Markdown bằng bộ chuyển đổi skill đã bật."), ["convert_document_to_markdown"], "ask");
    }

    private void BuildSkillIntegrations()
    {
        SkillIntegrations.Clear();
        foreach (var skill in SkillCatalog.All)
            SkillIntegrations.Add(new SkillIntegrationRow(skill.Id, LocalizeSkillName(skill.Id, skill.Name), LocalizeSkillKind(skill.Kind), LocalizeSkillDescription(skill.Id, skill.Description),
                skill.Instruction, SkillCatalog.IsEnabled(_settings, skill.Id)));
    }

    private static string ToThemeDisplay(string? themeMode) => themeMode?.ToLowerInvariant() switch
    {
        "light" => Localize("Light", "Sáng"), "dark" => Localize("Dark", "Tối"), _ => Localize("System", "Hệ thống")
    };

    private void AddPermission(string label, string description, IReadOnlyList<string> actions, string defaultPolicy)
    {
        var value = _settings.Permissions.GetValueOrDefault(actions[0], defaultPolicy) switch
        { "allowed" => Localize("Allowed", "Cho phép"), "denied" => Localize("Blocked", "Chặn"), _ => Localize("Ask", "Hỏi") };
        Permissions.Add(new PermissionRow(label, description, actions, value));
    }

    private void SetAgentMode(AgentRunMode mode)
    {
        if (_agentMode == mode) return;
        _agentMode = mode;
        OnPropertyChanged(nameof(IsCreateNewMode));
        OnPropertyChanged(nameof(IsWorkspaceContextMode));
        OnPropertyChanged(nameof(IsImproveFileMode));
        OnPropertyChanged(nameof(RequiresAgentSource));
        OnPropertyChanged(nameof(AgentStartLabel));
        OnPropertyChanged(nameof(AgentRequirementsText));
        OnPropertyChanged(nameof(CanStartAgent));
        if (!IsAgentRunning)
        {
            SetReadyStatus();
        }
    }

    private bool HasValidAgentSource()
    {
        if (!HasWorkspace || !File.Exists(AgentSourcePath)) return false;
        try
        {
            _ = new WorkspaceGuard(_settings.WorkspacePath!).EnsureInside(AgentSourcePath, false);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or DeskBridge.Core.Actions.DeskBridgeActionException)
        {
            return false;
        }
    }

    private async Task RefreshAssetsAsync()
    {
        Assets.Clear();
        if (!HasWorkspace) return;
        var assetDirectories = Directory.EnumerateDirectories(_settings.WorkspacePath!, "assets", SearchOption.AllDirectories);
        var paths = assetDirectories.SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Where(path => new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).Take(200);
        foreach (var path in paths)
        {
            var dimensions = Localize("Unknown size", "Chưa rõ kích thước");
            try
            {
                await using var stream = File.OpenRead(path);
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                dimensions = $"{frame.PixelWidth} × {frame.PixelHeight}";
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException) { }
            var bytes = new FileInfo(path).Length;
            Assets.Add(new AssetRow(Path.GetFileName(path), dimensions,
                bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.0} MB" : $"{Math.Max(1, bytes / 1024d):0} KB", path));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    private void BuildLocalizedOptions()
    {
        Policies.Clear();
        foreach (var value in new[] { Localize("Allowed", "Cho phép"), Localize("Ask", "Hỏi"), Localize("Blocked", "Chặn") }) Policies.Add(value);
        ThemeModes.Clear();
        foreach (var value in new[] { Localize("System", "Hệ thống"), Localize("Light", "Sáng"), Localize("Dark", "Tối") }) ThemeModes.Add(value);
    }

    private void SetReadyStatus()
    {
        AgentStatus = _agentMode == AgentRunMode.CreateNew ? Localize("Ready to create", "Sẵn sàng tạo mới") :
            _agentMode == AgentRunMode.WorkspaceContext ? Localize("Ready with protected context", "Sẵn sàng dùng ngữ cảnh được bảo vệ") : Localize("Ready to improve a file", "Sẵn sàng cải thiện tệp");
        AgentStatusDetail = _agentMode == AgentRunMode.CreateNew
            ? Localize("Describe the new result you want. The workspace is only an output boundary and no files will be uploaded.", "Mô tả kết quả mới bạn muốn. Không gian làm việc chỉ là nơi nhận đầu ra và không có tệp nào được tải lên.")
            : _agentMode == AgentRunMode.WorkspaceContext
                ? Localize("Describe the result. ChatGPT may request bounded read-only context; sensitive files and write actions remain blocked.", "Mô tả kết quả. ChatGPT có thể yêu cầu ngữ cảnh chỉ đọc có giới hạn; tệp nhạy cảm và thao tác ghi vẫn bị chặn.")
            : Localize("Choose one file inside the workspace and describe the finished result you want.", "Chọn một tệp trong không gian làm việc và mô tả kết quả hoàn chỉnh bạn muốn.");
    }

    private static string ToThemeId(string displayMode) => displayMode switch { "Light" or "Sáng" => "light", "Dark" or "Tối" => "dark", _ => "system" };
    private static string ToPolicyId(string displayPolicy) => displayPolicy switch { "Allowed" or "Cho phép" => "allowed", "Blocked" or "Chặn" => "denied", _ => "ask" };
    private static string Localize(string english, string vietnamese) => LocalizationService.Vietnamese ? vietnamese : english;
    private static string LocalizeSkillName(string id, string fallback) => id == SkillCatalog.ConvertDocumentsToMarkdown ? Localize(fallback, "Chuyển tài liệu sang Markdown") : fallback;
    private static string LocalizeSkillKind(string kind) => kind switch { "Executable adapter" => Localize(kind, "Bộ chuyển đổi thực thi"), "Guidance profile" => Localize(kind, "Hồ sơ hướng dẫn"), _ => kind };
    private static string LocalizeSkillDescription(string id, string fallback) => id switch
    {
        SkillCatalog.ConvertDocumentsToMarkdown => Localize(fallback, "Chuyển tài liệu được hỗ trợ trong không gian làm việc bằng @firecrawl/anydoc. Cần Node.js 20+ và npm; lần chạy đầu có thể tải bộ chuyển đổi từ npm."),
        SkillCatalog.Impeccable => Localize(fallback, "Thêm hướng dẫn chất lượng giao diện có thể tái sử dụng cho ChatGPT. DeskBridge không chạy mô hình thiết kế cục bộ."),
        _ => fallback
    };

    private static string LocalizeAgentText(string value)
    {
        if (!LocalizationService.Vietnamese) return value;
        return value switch
        {
            "Queued" => "Đã xếp hàng", "Waiting for ChatGPT Web." => "Đang chờ ChatGPT Web.", "Connected" => "Đã kết nối",
            "ChatGPT Web claimed this file task." => "ChatGPT Web đã nhận tác vụ xử lý tệp.", "ChatGPT Web claimed this create-new task." => "ChatGPT Web đã nhận tác vụ tạo mới.",
            "ChatGPT Web claimed this read-only workspace task." => "ChatGPT Web đã nhận tác vụ workspace chỉ đọc.", "Context ready" => "Ngữ cảnh sẵn sàng",
            "Cancelled" => "Đã hủy", "Web failed" => "Web gặp lỗi", "Verified" => "Đã xác minh", "Idea ready" => "Ý tưởng sẵn sàng",
            "ChatGPT Web" => "ChatGPT Web", "Local verification" => "Kiểm tra cục bộ", "Completed" => "Hoàn tất", "Review needed" => "Cần xem lại",
            _ => value
        };
    }

    private static void OpenExplorerTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { "/select,", path } });
        else if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { path } });
    }
}
