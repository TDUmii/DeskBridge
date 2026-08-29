using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using DeskBridge.App.Models;
using DeskBridge.Core.Agent;
using DeskBridge.Core.Models;
using DeskBridge.Core.Services;
using DeskBridge.Core.Skills;

namespace DeskBridge.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ActivityLogger _activityLogger = new();
    private readonly IApiKeyStore _apiKeyStore = new WindowsApiKeyStore();
    private readonly HttpClient _openAiHttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
    private DeskBridgeSettings _settings = new();
    private string _workspace = "No workspace selected";
    private string _selectedTheme = "System";
    private string _agentSourcePath = string.Empty;
    private string _agentRequest = string.Empty;
    private string _agentModel = "gpt-5.6-luna";
    private string _agentReasoning = "low";
    private int _agentIterations = 4;
    private string _agentStatus = "Ready for a file task";
    private string _agentStatusDetail = "Choose a file inside the workspace and describe the finished result you want.";
    private bool _isAgentRunning;
    private bool _apiKeyConfigured;
    private string? _agentResultPath;
    private string? _agentRunDirectory;
    private CancellationTokenSource? _agentCancellation;

    public string Workspace { get => _workspace; private set { _workspace = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWorkspace)); OnPropertyChanged(nameof(CanStartAgent)); } }
    public bool HasWorkspace => !string.IsNullOrWhiteSpace(_settings.WorkspacePath) && Directory.Exists(_settings.WorkspacePath);
    public ObservableCollection<ActivityEntry> Activities { get; } = [];
    public ObservableCollection<AssetRow> Assets { get; } = [];
    public ObservableCollection<PermissionRow> Permissions { get; } = [];
    public ObservableCollection<SkillIntegrationRow> SkillIntegrations { get; } = [];
    public ObservableCollection<AgentStepRow> AgentSteps { get; } = [];
    public string[] Policies { get; } = ["Allowed", "Ask", "Blocked"];
    public string[] ThemeModes { get; } = ["System", "Light", "Dark"];
    public string[] AgentModels { get; } = ["gpt-5.6-luna", "gpt-5.6-terra", "gpt-5.6-sol"];
    public string[] AgentReasoningModes { get; } = ["none", "low", "medium", "high", "xhigh", "max"];
    public int[] AgentIterationOptions { get; } = [2, 3, 4, 5, 6, 8];
    public string SelectedTheme { get => _selectedTheme; set { _selectedTheme = value; OnPropertyChanged(); } }
    public string AgentSourcePath { get => _agentSourcePath; set { _agentSourcePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartAgent)); } }
    public string AgentRequest { get => _agentRequest; set { _agentRequest = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartAgent)); } }
    public string AgentModel { get => _agentModel; set { _agentModel = value; OnPropertyChanged(); } }
    public string AgentReasoning { get => _agentReasoning; set { _agentReasoning = value; OnPropertyChanged(); } }
    public int AgentIterations { get => _agentIterations; set { _agentIterations = value; OnPropertyChanged(); } }
    public string AgentStatus { get => _agentStatus; private set { _agentStatus = value; OnPropertyChanged(); } }
    public string AgentStatusDetail { get => _agentStatusDetail; private set { _agentStatusDetail = value; OnPropertyChanged(); } }
    public bool IsAgentRunning { get => _isAgentRunning; private set { _isAgentRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartAgent)); } }
    public bool ApiKeyConfigured { get => _apiKeyConfigured; private set { _apiKeyConfigured = value; OnPropertyChanged(); OnPropertyChanged(nameof(ApiKeyStatus)); OnPropertyChanged(nameof(CanStartAgent)); } }
    public string ApiKeyStatus => ApiKeyConfigured ? "OpenAI API key is saved for this Windows account." : "No OpenAI API key is configured.";
    public bool CanStartAgent => HasWorkspace && ApiKeyConfigured && !IsAgentRunning && File.Exists(AgentSourcePath) && !string.IsNullOrWhiteSpace(AgentRequest);
    public bool HasAgentResult => !string.IsNullOrWhiteSpace(_agentResultPath) && File.Exists(_agentResultPath);
    public bool HasAgentRun => !string.IsNullOrWhiteSpace(_agentRunDirectory) && Directory.Exists(_agentRunDirectory);
    public bool IsInitialized { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        Workspace = string.IsNullOrWhiteSpace(_settings.WorkspacePath) ? "No workspace selected" : _settings.WorkspacePath;
        SelectedTheme = ToThemeDisplay(_settings.ThemeMode);
        AgentModel = _settings.Agent.Model;
        AgentReasoning = _settings.Agent.ReasoningEffort;
        AgentIterations = _settings.Agent.MaximumIterations;
        ApiKeyConfigured = _apiKeyStore.HasKey;
        BuildPermissions();
        BuildSkillIntegrations();
        await RefreshAsync();
        IsInitialized = true;
    }

    public async Task SetWorkspaceAsync(string path)
    {
        _settings = _settings with { WorkspacePath = Path.GetFullPath(path), WorkspaceMode = true };
        await _settingsStore.SaveAsync(_settings);
        Workspace = _settings.WorkspacePath;
        OnPropertyChanged(nameof(CanStartAgent));
        await RefreshAssetsAsync();
    }

    public async Task SaveApiKeyAsync(string apiKey)
    {
        await _apiKeyStore.SaveAsync(apiKey);
        ApiKeyConfigured = true;
        AgentStatus = "API key saved";
        AgentStatusDetail = "The key is encrypted for the current Windows account and is never written to settings or logs.";
    }

    public async Task TestApiKeyAsync()
    {
        _ = await _apiKeyStore.LoadAsync() ?? throw new InvalidOperationException("Save an OpenAI API key first.");
        var client = new OpenAIResponsesClient(_openAiHttpClient, _apiKeyStore);
        await client.TestConnectionAsync(AgentModel, CancellationToken.None);
        AgentStatus = "OpenAI connection verified";
        AgentStatusDetail = "DeskBridge can reach the Responses API with the saved key.";
    }

    public void RemoveApiKey()
    {
        _apiKeyStore.Delete();
        ApiKeyConfigured = _apiKeyStore.HasKey;
        AgentStatus = "Saved API key removed";
        AgentStatusDetail = ApiKeyConfigured ? "OPENAI_API_KEY is still provided by the environment." : "Add a key before starting an agent run.";
    }

    public async Task<AgentRunResult?> StartAgentAsync()
    {
        if (!CanStartAgent) return null;
        await SaveAgentSettingsAsync();
        _ = await _apiKeyStore.LoadAsync() ?? throw new InvalidOperationException("Save an OpenAI API key first.");
        _agentCancellation = new CancellationTokenSource();
        IsAgentRunning = true;
        _agentResultPath = null;
        _agentRunDirectory = null;
        OnPropertyChanged(nameof(HasAgentResult));
        OnPropertyChanged(nameof(HasAgentRun));
        AgentSteps.Clear();
        AgentStatus = "Starting agent";
        AgentStatusDetail = "Preparing the local inspection and encrypted API session.";
        var progress = new Progress<AgentProgress>(item =>
        {
            AgentStatus = item.Stage;
            AgentStatusDetail = item.Message;
            AgentSteps.Insert(0, new AgentStepRow(DateTime.Now.ToString("HH:mm:ss"), item.Stage, item.Message, item.Detail));
        });

        try
        {
            var client = new OpenAIResponsesClient(_openAiHttpClient, _apiKeyStore);
            var service = new AgentRunService(client);
            var options = new AgentRunOptions(AgentModel, AgentReasoning, AgentIterations,
                _settings.Agent.MaximumToolCalls, _settings.Agent.MaximumOutputTokensPerTurn);
            var result = await service.RunAsync(new AgentRunRequest(_settings.WorkspacePath!, AgentSourcePath, AgentRequest, options), progress, _agentCancellation.Token);
            _agentResultPath = result.BestArtifactPath;
            _agentRunDirectory = result.RunDirectory;
            AgentStatus = result.Success ? "Finished" : "Best candidate preserved";
            AgentStatusDetail = $"{result.Summary}  Token usage: {result.Usage.TotalTokens:N0}.";
            OnPropertyChanged(nameof(HasAgentResult));
            OnPropertyChanged(nameof(HasAgentRun));
            return result;
        }
        catch (OperationCanceledException)
        {
            AgentStatus = "Cancelled";
            AgentStatusDetail = "The current request was stopped. The original file was not changed.";
            return null;
        }
        catch (Exception exception)
        {
            AgentStatus = "Needs attention";
            AgentStatusDetail = exception.Message;
            AgentSteps.Insert(0, new AgentStepRow(DateTime.Now.ToString("HH:mm:ss"), "Error", exception.Message, null));
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
            permissions[action] = row.Policy switch { "Allowed" => "allowed", "Blocked" => "denied", _ => "ask" };
        _settings = _settings with { Permissions = permissions };
        await _settingsStore.SaveAsync(_settings);
    }

    public async Task SaveThemeAsync(string displayMode)
    {
        SelectedTheme = displayMode;
        _settings = _settings with { ThemeMode = displayMode.ToLowerInvariant() };
        await _settingsStore.SaveAsync(_settings);
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
        _settings = _settings with { Agent = _settings.Agent with { Model = AgentModel, ReasoningEffort = AgentReasoning, MaximumIterations = AgentIterations } };
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
        AddPermission("Read files", "Read files and inspect folders in the workspace.", ["read_file", "list_folder"], "allowed");
        AddPermission("Write files", "Create, update, and patch workspace content.", ["write_file", "create_file", "create_folder", "create_project", "update_project", "patch_file"], "ask");
        AddPermission("Run commands", "Run an allowed program with an argument list.", ["run_command"], "ask");
        AddPermission("Assets", "Download, import, resize, compress, and convert images.", ["download_asset", "import_asset", "resize_image", "compress_image", "convert_image"], "ask");
        AddPermission("Screenshots", "Capture the primary monitor to a local temp file.", ["capture_screen"], "ask");
        AddPermission("Document conversion", "Convert a supported workspace document to Markdown with the enabled skill adapter.", ["convert_document_to_markdown"], "ask");
    }

    private void BuildSkillIntegrations()
    {
        SkillIntegrations.Clear();
        foreach (var skill in SkillCatalog.All)
            SkillIntegrations.Add(new SkillIntegrationRow(skill.Id, skill.Name, skill.Kind, skill.Description,
                skill.Instruction, SkillCatalog.IsEnabled(_settings, skill.Id)));
    }

    private static string ToThemeDisplay(string? themeMode) => themeMode?.ToLowerInvariant() switch
    {
        "light" => "Light", "dark" => "Dark", _ => "System"
    };

    private void AddPermission(string label, string description, IReadOnlyList<string> actions, string defaultPolicy)
    {
        var value = _settings.Permissions.GetValueOrDefault(actions[0], defaultPolicy) switch
        { "allowed" => "Allowed", "denied" => "Blocked", _ => "Ask" };
        Permissions.Add(new PermissionRow(label, description, actions, value));
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
            var dimensions = "Unknown size";
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

    private static void OpenExplorerTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { "/select,", path } });
        else if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { path } });
    }
}
