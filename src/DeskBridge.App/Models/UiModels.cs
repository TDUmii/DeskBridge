using System.ComponentModel;

namespace DeskBridge.App.Models;

public sealed class PermissionRow : INotifyPropertyChanged
{
    private string _policy;
    public PermissionRow(string label, string description, IReadOnlyList<string> actions, string policy) =>
        (Label, Description, Actions, _policy) = (label, description, actions, policy);
    public string Label { get; }
    public string Description { get; }
    public IReadOnlyList<string> Actions { get; }
    public string Policy
    {
        get => _policy;
        set { if (_policy == value) return; _policy = value; PropertyChanged?.Invoke(this, new(nameof(Policy))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record AssetRow(string Name, string Dimensions, string Size, string Path);

public sealed class SkillIntegrationRow : INotifyPropertyChanged
{
    private bool _enabled;
    public SkillIntegrationRow(string id, string name, string kind, string description, string instruction, bool enabled) =>
        (Id, Name, Kind, Description, Instruction, _enabled) = (id, name, kind, description, instruction, enabled);
    public string Id { get; }
    public string Name { get; }
    public string Kind { get; }
    public string Description { get; }
    public string Instruction { get; }
    public bool Enabled
    {
        get => _enabled;
        set { if (_enabled == value) return; _enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
