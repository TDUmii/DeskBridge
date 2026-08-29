import { DeskBridgeRequest, DeskBridgeResponse, parseRequest } from "../shared/protocol.js";

const hostName = "com.deskbridge.host";
let port: chrome.runtime.Port | null = null;
const pending = new Map<string, { resolve: (value: DeskBridgeResponse) => void; timer: number }>();
const downloads = new Map<string, { expected: string; downloadId?: number; path?: string; state: "armed" | "downloading" | "complete" | "failed"; error?: string; timer: number }>();

function connect(): chrome.runtime.Port {
  if (port) return port;
  port = chrome.runtime.connectNative(hostName);
  port.onMessage.addListener((response: DeskBridgeResponse) => {
    const item = pending.get(response.id);
    if (!item) return;
    clearTimeout(item.timer);
    pending.delete(response.id);
    item.resolve(response);
  });
  port.onDisconnect.addListener(() => {
    const message = chrome.runtime.lastError?.message ?? "Native host disconnected.";
    for (const [id, item] of pending) {
      clearTimeout(item.timer);
      item.resolve({ version: 1, id, success: false, error: { code: "EXECUTION_FAILED", message } });
    }
    pending.clear();
    port = null;
  });
  return port;
}

function send(request: DeskBridgeRequest): Promise<DeskBridgeResponse> {
  parseRequest(JSON.stringify(request));
  return new Promise((resolve) => {
    const timer = self.setTimeout(() => {
      pending.delete(request.id);
      resolve({ version: 1, id: request.id, success: false,
        error: { code: "COMMAND_TIMEOUT", message: "DeskBridge did not respond within 5 minutes." } });
    }, 300_000);
    pending.set(request.id, { resolve, timer });
    connect().postMessage(request);
  });
}

chrome.downloads.onCreated.addListener((item) => {
  const name = item.filename.split(/[\\/]/).pop() ?? "";
  const watch = Array.from(downloads.entries()).find(([, value]) => value.state === "armed" && value.expected.toLowerCase() === name.toLowerCase());
  if (!watch) return;
  const [token, value] = watch;
  downloads.set(token, { ...value, downloadId: item.id, path: item.filename, state: "downloading" });
});

chrome.downloads.onChanged.addListener(async (delta) => {
  const watch = Array.from(downloads.entries()).find(([, value]) => value.downloadId === delta.id);
  if (!watch) return;
  const [token, value] = watch;
  if (delta.error?.current) {
    downloads.set(token, { ...value, state: "failed", error: delta.error.current });
    return;
  }
  if (delta.state?.current !== "complete") return;
  const items = await chrome.downloads.search({ id: delta.id });
  const item = items[0];
  downloads.set(token, { ...value, path: item?.filename ?? value.path, state: item ? "complete" : "failed", error: item ? undefined : "Completed download could not be located." });
});

function request(action: string, args: Record<string, unknown>): DeskBridgeRequest {
  return { version: 1, id: `${action}-${crypto.randomUUID()}`, action, arguments: args };
}

chrome.runtime.onMessage.addListener((message: unknown, _sender, sendResponse) => {
  if (!message || typeof message !== "object") return false;
  const envelope = message as { type?: string; request?: DeskBridgeRequest; runId?: string; offset?: number; maxBytes?: number;
    stage?: string; message?: string; chatUrl?: string; downloadedPath?: string; assessment?: unknown; expectedFilename?: string; token?: string };
  if (envelope.type === "disconnect") {
    port?.disconnect(); port = null; sendResponse({ success: true }); return false;
  }
  if (envelope.type === "run" && envelope.request) { send(envelope.request).then(sendResponse); return true; }
  if (envelope.type === "webAgentClaim") { send(request("web_agent_claim", {})).then(sendResponse); return true; }
  if (envelope.type === "webAgentSourceChunk") {
    send(request("web_agent_source_chunk", { runId: envelope.runId, offset: envelope.offset, maxBytes: envelope.maxBytes })).then(sendResponse); return true;
  }
  if (envelope.type === "webAgentProgress") {
    send(request("web_agent_progress", { runId: envelope.runId, stage: envelope.stage, message: envelope.message, chatUrl: envelope.chatUrl })).then(sendResponse); return true;
  }
  if (envelope.type === "webAgentCandidate") {
    send(request("web_agent_candidate", { runId: envelope.runId, downloadedPath: envelope.downloadedPath, assessment: envelope.assessment })).then(sendResponse); return true;
  }
  if (envelope.type === "webAgentFail") {
    send(request("web_agent_fail", { runId: envelope.runId, message: envelope.message })).then(sendResponse); return true;
  }
  if (envelope.type === "armDownload" && envelope.expectedFilename) {
    const token = crypto.randomUUID();
    const timer = self.setTimeout(() => {
      const current = downloads.get(token);
      if (current && current.state !== "complete") downloads.set(token, { ...current, state: "failed", error: "ChatGPT Web download timed out." });
    }, 180_000);
    downloads.set(token, { expected: envelope.expectedFilename, state: "armed", timer });
    sendResponse({ success: true, token }); return false;
  }
  if (envelope.type === "downloadStatus" && envelope.token) {
    const value = downloads.get(envelope.token);
    if (!value) { sendResponse({ state: "failed", error: "Unknown download watch." }); return false; }
    if (value.state === "complete" || value.state === "failed") {
      clearTimeout(value.timer); downloads.delete(envelope.token);
    }
    sendResponse({ state: value.state, path: value.path, error: value.error }); return false;
  }
  return false;
});
