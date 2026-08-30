import { DeskBridgeResponse, newId } from "../shared/protocol.js";
import { Language, getLanguage, setLanguage, t } from "../shared/i18n.js";

const status = document.querySelector<HTMLSpanElement>("#status")!;
const workspace = document.querySelector<HTMLElement>("#workspace")!;
const error = document.querySelector<HTMLDivElement>("#error")!;
const skills = document.querySelector<HTMLElement>("#skills")!;
const languagePicker = document.querySelector<HTMLSelectElement>("#language")!;
let language: Language = "en";

function applyLanguage(): void {
  document.documentElement.lang = language;
  const ids: Record<string, string> = { subtitle: "subtitle", status: "checking", "workspace-label": "workspaceScope", workspace: "unavailable", "transport-label": "transport", "web-only": "webOnly", "web-guard": "webGuard", "no-fallback": "noFallback", "skills-label": "enabledSkills", skills: "none", open: "open", disconnect: "disconnect", "language-label": "language" };
  for (const [id, key] of Object.entries(ids)) document.querySelector<HTMLElement>(`#${id}`)!.textContent = t(language, key);
}

async function refresh(): Promise<void> {
  const response = await chrome.runtime.sendMessage({ type: "run", request: {
    version: 1, id: newId("status"), action: "get_status", arguments: {}
  } }) as DeskBridgeResponse;
  if (response.success) {
    const data = response.data as { workspace?: string; skills?: Array<{ name: string; enabled: boolean }> };
    status.textContent = t(language, "connected");
    status.className = "status connected";
    workspace.textContent = data.workspace ?? t(language, "noWorkspace");
    skills.textContent = data.skills?.filter(skill => skill.enabled).map(skill => skill.name).join(", ") || t(language, "none");
    error.hidden = true;
  } else {
    status.textContent = t(language, "disconnected");
    status.className = "status error";
    error.hidden = false;
    error.textContent = response.error?.message ?? t(language, "bridgeUnavailable");
  }
}

document.querySelector("#disconnect")!.addEventListener("click", async () => {
  await chrome.runtime.sendMessage({ type: "disconnect" });
  status.textContent = t(language, "disconnected");
  status.className = "status error";
});

document.querySelector("#open")!.addEventListener("click", async () => {
  const response = await chrome.runtime.sendMessage({ type: "run", request: {
    version: 1, id: newId("open"), action: "open_deskbridge", arguments: {}
  } }) as DeskBridgeResponse;
  if (!response.success) {
    error.hidden = false;
    error.textContent = response.error?.message ?? t(language, "openFailed");
  }
});

languagePicker.addEventListener("change", async () => {
  language = languagePicker.value === "vi" ? "vi" : "en";
  await setLanguage(language);
  applyLanguage();
  await refresh();
});

void (async () => {
  language = await getLanguage();
  languagePicker.value = language;
  applyLanguage();
  await refresh();
})();
