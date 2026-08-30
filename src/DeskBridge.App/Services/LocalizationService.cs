using System.Windows;

namespace DeskBridge.App.Services;

public sealed class LocalizationService
{
    private static string _language = "en";

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["AppSubtitle"] = "Local verification for ChatGPT Web creation and file work",
        ["ControlReady"] = "Control panel ready",
        ["TabAgent"] = "Agent", ["TabWorkspace"] = "Workspace", ["TabActivity"] = "Activity", ["TabAssets"] = "Assets", ["TabSettings"] = "Settings",
        ["AgentHeading"] = "Turn an idea or file into a verified result",
        ["AgentIntro"] = "Create from a request alone or improve one selected file. DeskBridge stays on ChatGPT Web, checks downloads locally, and never shares the rest of your workspace.",
        ["ChooseStart"] = "Choose how to start", ["ChooseStartHelp"] = "Create something new from the idea alone, or attach exactly one file to improve.",
        ["CreateNew"] = "Create new", ["CreateNewHelp"] = "Request only. No workspace uploads.",
        ["ImproveFile"] = "Improve a file", ["ImproveFileHelp"] = "Only the selected file is uploaded.",
        ["OutputWorkspace"] = "Output workspace", ["NoPathsSent"] = "No files or local paths are sent to ChatGPT Web.", ["RequestOnly"] = "Request only",
        ["ChooseFile"] = "Choose file", ["DescribeResult"] = "Describe the finished result", ["DescribeResultHelp"] = "Describe the finished downloadable result and its quality.",
        ["Cancel"] = "Cancel", ["OpenResult"] = "Open result", ["SolGuard"] = "If GPT-5.6 Sol with High reasoning cannot be verified, nothing is sent.",
        ["CurrentRun"] = "Current run", ["OpenEvidence"] = "Open run evidence", ["WebModel"] = "Web model", ["Reasoning"] = "Reasoning", ["MaximumPasses"] = "Maximum passes", ["RunTimeline"] = "Run timeline",
        ["CurrentWorkspace"] = "Current workspace", ["ChooseWorkspace"] = "Choose workspace", ["OpenFolder"] = "Open folder",
        ["ProtectedActions"] = "How protected actions run", ["ProtectedActionsHelp"] = "ChatGPT action blocks never run automatically. The native host validates the action, permission, workspace boundary, and action-specific arguments before execution.",
        ["RecentActivity"] = "Recent activity", ["ActivityHelp"] = "Local actions and verification results", ["Refresh"] = "Refresh",
        ["ColTime"] = "Time", ["ColAction"] = "Action", ["ColTarget"] = "Target", ["ColResult"] = "Result",
        ["WorkspaceAssets"] = "Workspace assets", ["AssetsHelp"] = "Images found below folders named assets. DeskBridge inspects but does not edit them here.", ["ColFile"] = "File", ["ColDimensions"] = "Dimensions", ["ColSize"] = "Size", ["ColPath"] = "Path",
        ["WebTransport"] = "ChatGPT Web transport", ["WebTransportHelp"] = "Browser-only is locked on. DeskBridge refuses Codex, Codex workspace, and Platform API fallback. No API key is requested or stored.",
        ["Appearance"] = "Appearance", ["Language"] = "Language",
        ["SkillIntegrations"] = "Skill integrations", ["SkillHelp"] = "Executable adapters perform bounded local actions. Guidance profiles provide instructions for ChatGPT and do not run a local AI model.", ["CopyInstruction"] = "Copy instruction", ["Enabled"] = "Enabled",
        ["Permissions"] = "Permissions", ["PermissionsHelp"] = "Ask is recommended for actions with side effects. There is no allow-everything setting.",
        ["LocalLogs"] = "Local logs", ["LogsHelp"] = "Review timestamps, actions, targets, results, and duration.", ["OpenLogs"] = "Open logs", ["ClearLogs"] = "Clear logs",
        ["FooterNative"] = "Native host: com.deskbridge.host  •  Chrome Native Messaging  •  No local HTTP listener", ["FooterBoundary"] = "Web-only boundary locked",
        ["PermissionTitle"] = "DeskBridge permission", ["PermissionRequired"] = "Permission required", ["PermissionReview"] = "Review the exact action before DeskBridge continues.", ["AllowOnceHelp"] = "Allow once applies only to this request. Change the per-action policy in Settings.", ["AllowOnce"] = "Allow once"
    };

    private static readonly IReadOnlyDictionary<string, string> VietnameseResources = new Dictionary<string, string>
    {
        ["AppSubtitle"] = "Kiểm tra cục bộ cho tác vụ tạo mới và xử lý tệp bằng ChatGPT Web",
        ["ControlReady"] = "Bảng điều khiển sẵn sàng",
        ["TabAgent"] = "Tác vụ", ["TabWorkspace"] = "Không gian làm việc", ["TabActivity"] = "Hoạt động", ["TabAssets"] = "Tài nguyên", ["TabSettings"] = "Cài đặt",
        ["AgentHeading"] = "Biến ý tưởng hoặc tệp thành kết quả đã được kiểm tra",
        ["AgentIntro"] = "Tạo mới chỉ từ yêu cầu hoặc cải thiện một tệp được chọn. DeskBridge luôn dùng ChatGPT Web, kiểm tra tệp tải xuống trên máy và không chia sẻ phần còn lại của không gian làm việc.",
        ["ChooseStart"] = "Chọn cách bắt đầu", ["ChooseStartHelp"] = "Tạo nội dung mới từ ý tưởng hoặc đính kèm đúng một tệp để cải thiện.",
        ["CreateNew"] = "Tạo mới", ["CreateNewHelp"] = "Chỉ gửi yêu cầu. Không tải tệp trong không gian làm việc lên.",
        ["ImproveFile"] = "Cải thiện tệp", ["ImproveFileHelp"] = "Chỉ tệp đã chọn được tải lên.",
        ["OutputWorkspace"] = "Không gian nhận kết quả", ["NoPathsSent"] = "Không gửi tệp hoặc đường dẫn cục bộ lên ChatGPT Web.", ["RequestOnly"] = "Chỉ gửi yêu cầu",
        ["ChooseFile"] = "Chọn tệp", ["DescribeResult"] = "Mô tả kết quả hoàn chỉnh", ["DescribeResultHelp"] = "Mô tả tệp kết quả cần tải xuống và tiêu chuẩn chất lượng mong muốn.",
        ["Cancel"] = "Hủy", ["OpenResult"] = "Mở kết quả", ["SolGuard"] = "Nếu không xác minh được GPT-5.6 Sol với mức suy luận Cao, hệ thống sẽ không gửi nội dung.",
        ["CurrentRun"] = "Tác vụ hiện tại", ["OpenEvidence"] = "Mở bằng chứng tác vụ", ["WebModel"] = "Mô hình web", ["Reasoning"] = "Suy luận", ["MaximumPasses"] = "Số lượt tối đa", ["RunTimeline"] = "Tiến trình tác vụ",
        ["CurrentWorkspace"] = "Không gian làm việc hiện tại", ["ChooseWorkspace"] = "Chọn không gian", ["OpenFolder"] = "Mở thư mục",
        ["ProtectedActions"] = "Cách chạy hành động được bảo vệ", ["ProtectedActionsHelp"] = "Khối hành động từ ChatGPT không bao giờ tự chạy. Native host kiểm tra hành động, quyền, giới hạn không gian làm việc và từng đối số trước khi thực thi.",
        ["RecentActivity"] = "Hoạt động gần đây", ["ActivityHelp"] = "Hành động cục bộ và kết quả kiểm tra", ["Refresh"] = "Làm mới",
        ["ColTime"] = "Thời gian", ["ColAction"] = "Hành động", ["ColTarget"] = "Đích", ["ColResult"] = "Kết quả",
        ["WorkspaceAssets"] = "Tài nguyên trong không gian", ["AssetsHelp"] = "Ảnh nằm trong các thư mục tên assets. DeskBridge chỉ kiểm tra và không chỉnh sửa tại đây.", ["ColFile"] = "Tệp", ["ColDimensions"] = "Kích thước", ["ColSize"] = "Dung lượng", ["ColPath"] = "Đường dẫn",
        ["WebTransport"] = "Kết nối ChatGPT Web", ["WebTransportHelp"] = "Chế độ chỉ dùng trình duyệt luôn được khóa. DeskBridge từ chối Codex, Codex workspace và phương án dự phòng qua Platform API. Không yêu cầu hoặc lưu API key.",
        ["Appearance"] = "Giao diện", ["Language"] = "Ngôn ngữ",
        ["SkillIntegrations"] = "Tích hợp skill", ["SkillHelp"] = "Bộ chuyển đổi thực thi các hành động cục bộ có giới hạn. Hồ sơ hướng dẫn cung cấp chỉ dẫn cho ChatGPT và không chạy mô hình AI trên máy.", ["CopyInstruction"] = "Sao chép hướng dẫn", ["Enabled"] = "Đã bật",
        ["Permissions"] = "Quyền", ["PermissionsHelp"] = "Nên dùng chế độ Hỏi với các hành động làm thay đổi dữ liệu. Không có tùy chọn cho phép mọi thứ.",
        ["LocalLogs"] = "Nhật ký cục bộ", ["LogsHelp"] = "Xem thời gian, hành động, đích, kết quả và thời lượng.", ["OpenLogs"] = "Mở nhật ký", ["ClearLogs"] = "Xóa nhật ký",
        ["FooterNative"] = "Native host: com.deskbridge.host  •  Chrome Native Messaging  •  Không có máy chủ HTTP cục bộ", ["FooterBoundary"] = "Đã khóa giới hạn chỉ dùng Web",
        ["PermissionTitle"] = "Quyền DeskBridge", ["PermissionRequired"] = "Cần cấp quyền", ["PermissionReview"] = "Kiểm tra chính xác hành động trước khi DeskBridge tiếp tục.", ["AllowOnceHelp"] = "Cho phép một lần chỉ áp dụng cho yêu cầu này. Có thể đổi chính sách của từng hành động trong Cài đặt.", ["AllowOnce"] = "Cho phép một lần"
    };

    public bool IsVietnamese => _language == "vi";
    public static bool Vietnamese => _language == "vi";
    public static string T(string key) => (_language == "vi" ? VietnameseResources : English).GetValueOrDefault(key, key);

    public void Apply(string displayMode)
    {
        _language = displayMode.Equals("Tiếng Việt", StringComparison.OrdinalIgnoreCase) || displayMode.Equals("vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en";
        var resources = Application.Current.Resources;
        foreach (var pair in (_language == "vi" ? VietnameseResources : English)) resources[pair.Key] = pair.Value;
    }
}
