export const allowedActions = new Set([
  "get_status", "open_deskbridge", "read_file", "write_file", "create_file", "create_folder", "list_folder",
  "create_project", "update_project", "patch_file", "get_clipboard", "set_clipboard",
  "open_folder", "open_app", "open_project", "open_in_browser", "preview_web", "run_command",
  "capture_screen", "get_active_window", "download_asset", "import_asset", "inspect_image",
  "resize_image", "compress_image", "convert_image", "get_skill_profile", "convert_document_to_markdown",
  "web_agent_claim", "web_agent_source_chunk", "web_agent_progress", "web_agent_candidate", "web_agent_fail"
]);

export interface DeskBridgeRequest {
  version: 1;
  id: string;
  action: string;
  arguments: Record<string, unknown>;
}

export interface DeskBridgeResponse {
  version: 1;
  id: string;
  success: boolean;
  data?: unknown;
  error?: { code: string; message: string };
}

export interface WebAgentClaim {
  runId: string;
  sourceFileName: string;
  sourceSize: number;
  prompt: string;
  requiredModel: string;
  requiredReasoning: string;
  maximumIterations: number;
  candidateToken: string;
}

export interface CandidateAssessment {
  candidateFile: string;
  score: number;
  summary: string;
  requirementsMet: string[];
  remainingIssues: string[];
}

export function parseRequest(text: string): DeskBridgeRequest {
  const value: unknown = JSON.parse(text);
  if (!value || typeof value !== "object") throw new Error("Action must be a JSON object.");
  const request = value as Partial<DeskBridgeRequest>;
  if (request.version !== 1 || typeof request.id !== "string" || request.id.trim() === "" ||
      typeof request.action !== "string" || !allowedActions.has(request.action) ||
      !request.arguments || typeof request.arguments !== "object" || Array.isArray(request.arguments)) {
    throw new Error("Invalid DeskBridge v1 request or unsupported action.");
  }
  return request as DeskBridgeRequest;
}

export function newId(prefix: string): string {
  return `${prefix}-${crypto.randomUUID()}`;
}
