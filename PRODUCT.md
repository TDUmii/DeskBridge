# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop with a Chrome companion.

## Stack

C# and .NET 8 power the WPF control panel, local verifier, and Chrome Native Messaging host. A dependency-light Manifest V3 extension uses TypeScript, HTML, and CSS to operate a dedicated signed-in ChatGPT Web tab.

## Product purpose

DeskBridge turns a normal file request into a bounded ChatGPT Web workflow. It preserves the selected source, opens a dedicated web tab, requires GPT-5.6 Sol with High reasoning, uploads through the visible ChatGPT page, downloads each proposed artifact, checks it locally, and asks the same web conversation for revisions until the completion gate passes or the configured pass limit is reached.

## Non-negotiable boundary

The Agent is ChatGPT Web-only. It never switches to Codex, creates or uses a Codex workspace/task, calls the OpenAI Platform API, asks for an API key, or reads browser cookies/session tokens. If the required web model or High level cannot be verified, the run stops safely.

## Operating context

The user signs in to ChatGPT normally in Chrome, installs the DeskBridge extension/native host, selects one local workspace and source file, then describes the finished outcome. The original remains unchanged. Accepted output appears in `DeskBridge Results`; every candidate and inspection record stays in a workspace-local evidence directory.

## Capabilities and constraints

- Windows 10/11, Google Chrome, and a signed-in ChatGPT Web account with GPT-5.6 Sol are required.
- Production communication uses Chrome Native Messaging only; DeskBridge exposes no local HTTP server.
- Only a tab opened with the DeskBridge agent marker may claim autonomous jobs; normal ChatGPT tabs are not polled for work.
- Model and reasoning are locked to GPT-5.6 Sol and High (3/3) and verified before sending.
- The selected source is transferred to the page through the visible file-upload control. No cookies, tokens, browser profile data, private endpoints, or API keys are accessed.
- Downloads require the run's unique filename token before local inspection.
- The original file cannot be overwritten. Completion requires a locally inspected candidate, score at least 90, and no declared remaining issue.
- Existing explicit action blocks remain permission- and workspace-guarded.

## Brand and experience

DeskBridge is calm, direct, and security-conscious. Its visual identity turns the workflow into a compact bridge: source file, verification checkpoint, then accepted output. Graphite surfaces, electric blue actions, cyan checkpoints, and restrained teal success states create a recognizable Windows operations panel without copying generic automation-console symbols. The app and extension share the same icon, color tokens, readable status, clear scope, keyboard focus, non-color state labels, cancellation, and honest failure guidance.
