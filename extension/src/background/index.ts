import { DeskBridgeRequest, DeskBridgeResponse, parseRequest } from "../shared/protocol.js";

const hostName = "com.deskbridge.host";
let port: chrome.runtime.Port | null = null;
const pending = new Map<string, { resolve: (value: DeskBridgeResponse) => void; timer: number }>();

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

chrome.runtime.onMessage.addListener((message: unknown, _sender, sendResponse) => {
  if (!message || typeof message !== "object") return false;
  const envelope = message as { type?: string; request?: DeskBridgeRequest };
  if (envelope.type === "disconnect") {
    port?.disconnect();
    port = null;
    sendResponse({ success: true });
    return false;
  }
  if (envelope.type === "run" && envelope.request) {
    send(envelope.request).then(sendResponse);
    return true;
  }
  return false;
});
