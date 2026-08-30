export type Language = "en" | "vi";

const messages: Record<Language, Record<string, string>> = {
  en: {
    subtitle: "Verified ChatGPT Web file agent", checking: "Checking...", connected: "● Connected",
    disconnected: "Disconnected", workspaceScope: "Workspace scope", unavailable: "Not available",
    noWorkspace: "No workspace selected", transport: "Protected transport", webOnly: "ChatGPT Web only",
    webGuard: "GPT-5.6 Sol - High 3/3 required", noFallback: "No Codex, workspace, or API fallback",
    enabledSkills: "Enabled skills", none: "None", open: "Open DeskBridge", disconnect: "Disconnect",
    language: "Language", bridgeUnavailable: "DeskBridge is unavailable.", openFailed: "Could not open DeskBridge.",
    run: "Run with DeskBridge", waiting: "Waiting for DeskBridge...", success: "Success", actionFailed: "Action failed.",
    invalidAction: "Invalid DeskBridge action.", copyResult: "Copy result", saveFile: "Save file",
    pathPrompt: "Absolute path inside the current DeskBridge workspace:", savedTo: "Saved to",
    saveImage: "Save to DeskBridge", chooseWorkspace: "Open DeskBridge and choose a workspace before saving this image.",
    destinationPrompt: "Destination path inside the current DeskBridge workspace:"
  },
  vi: {
    subtitle: "Trợ lý tệp ChatGPT Web có kiểm tra cục bộ", checking: "Đang kiểm tra...", connected: "● Đã kết nối",
    disconnected: "Chưa kết nối", workspaceScope: "Phạm vi làm việc", unavailable: "Chưa khả dụng",
    noWorkspace: "Chưa chọn không gian làm việc", transport: "Kết nối được bảo vệ", webOnly: "Chỉ dùng ChatGPT Web",
    webGuard: "Yêu cầu GPT-5.6 Sol - Cao 3/3", noFallback: "Không dùng Codex, workspace hoặc API dự phòng",
    enabledSkills: "Skill đã bật", none: "Không có", open: "Mở DeskBridge", disconnect: "Ngắt kết nối",
    language: "Ngôn ngữ", bridgeUnavailable: "DeskBridge hiện không khả dụng.", openFailed: "Không thể mở DeskBridge.",
    run: "Chạy bằng DeskBridge", waiting: "Đang chờ DeskBridge...", success: "Thành công", actionFailed: "Hành động thất bại.",
    invalidAction: "Hành động DeskBridge không hợp lệ.", copyResult: "Sao chép kết quả", saveFile: "Lưu tệp",
    pathPrompt: "Đường dẫn tuyệt đối trong không gian làm việc DeskBridge hiện tại:", savedTo: "Đã lưu vào",
    saveImage: "Lưu vào DeskBridge", chooseWorkspace: "Mở DeskBridge và chọn không gian làm việc trước khi lưu ảnh.",
    destinationPrompt: "Đường dẫn đích trong không gian làm việc DeskBridge hiện tại:"
  }
};

export function t(language: Language, key: string): string { return messages[language][key] ?? messages.en[key] ?? key; }
export async function getLanguage(): Promise<Language> {
  const stored = await chrome.storage.local.get("deskbridge.language");
  return stored["deskbridge.language"] === "vi" ? "vi" : "en";
}
export async function setLanguage(language: Language): Promise<void> {
  await chrome.storage.local.set({ "deskbridge.language": language });
}
