import { DeskBridgeResponse, newId } from "../shared/protocol.js";

const status = document.querySelector<HTMLSpanElement>("#status")!;
const workspace = document.querySelector<HTMLElement>("#workspace")!;
const error = document.querySelector<HTMLDivElement>("#error")!;

async function refresh(): Promise<void> {
  const response = await chrome.runtime.sendMessage({ type: "run", request: {
    version: 1, id: newId("status"), action: "get_status", arguments: {}
  } }) as DeskBridgeResponse;
  if (response.success) {
    const data = response.data as { workspace?: string };
    status.textContent = "● Connected";
    status.className = "status connected";
    workspace.textContent = data.workspace ?? "No workspace selected";
    error.hidden = true;
  } else {
    status.textContent = "Disconnected";
    status.className = "status error";
    error.hidden = false;
    error.textContent = response.error?.message ?? "DeskBridge is unavailable.";
  }
}

document.querySelector("#disconnect")!.addEventListener("click", async () => {
  await chrome.runtime.sendMessage({ type: "disconnect" });
  status.textContent = "Disconnected";
  status.className = "status error";
});

document.querySelector("#open")!.addEventListener("click", async () => {
  const response = await chrome.runtime.sendMessage({ type: "run", request: {
    version: 1, id: newId("open"), action: "open_deskbridge", arguments: {}
  } }) as DeskBridgeResponse;
  if (!response.success) {
    error.hidden = false;
    error.textContent = response.error?.message ?? "Could not open DeskBridge.";
  }
});

void refresh();
