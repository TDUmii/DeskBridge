# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop with a Chrome companion.

## Stack

C# and .NET 8 power the WPF control panel, local verifier, and Chrome Native Messaging host. A dependency-light Manifest V3 extension uses TypeScript, HTML, and CSS to operate a dedicated signed-in ChatGPT Web tab.

## Product purpose

DeskBridge turns an idea, project-level request, or file request into a bounded ChatGPT Web workflow. It can create from the request alone, expose selective read-only workspace context, or preserve and upload one explicitly selected source. It opens a dedicated web tab, requires GPT-5.6 Sol with High reasoning, downloads each proposed artifact, checks it locally, and asks the same web conversation for revisions until the completion gate passes or the configured pass limit is reached.

## Non-negotiable boundary

The Agent is ChatGPT Web-only. It never switches to Codex, creates or uses a Codex workspace/task, calls the OpenAI Platform API, asks for an API key, or reads browser cookies/session tokens. If the required web model or High level cannot be verified, the run stops safely.

## Operating context

The user runs one self-contained Windows installer, completes Chrome's required visible extension confirmation, signs in to ChatGPT normally, selects one local workspace, chooses create-new, workspace-context, or improve-file mode, then describes the finished outcome. Create-new uploads no workspace files or local paths. Workspace-context returns only narrow native-filtered reads with virtual paths. Improve-file uploads only the explicit source and keeps the original unchanged. Accepted output appears in `DeskBridge Results`; every candidate and inspection record stays in a workspace-local evidence directory.

## Capabilities and constraints

- Windows 10/11, Google Chrome, and a signed-in ChatGPT Web account with GPT-5.6 Sol are required.
- The installer requires no administrator access, installs for the current Windows user, registers uninstall metadata, and preserves user workspaces during upgrades or removal.
- Chrome requires one visible confirmation to load the unpacked companion extension; DeskBridge never edits browser profile data to bypass that boundary.
- Production communication uses Chrome Native Messaging only; DeskBridge exposes no local HTTP server.
- Only a tab opened with the DeskBridge agent marker may claim autonomous jobs; normal ChatGPT tabs are not polled for work.
- Model and reasoning are locked to GPT-5.6 Sol and High (3/3) and verified before sending.
- Create-new sends only the user's request. Improve-file transfers only the selected source through the visible file-upload control. No cookies, tokens, browser profile data, private endpoints, or API keys are accessed.
- Workspace-context permits at most four read-only requests per round and six rounds. It blocks sensitive paths, ignored content, writes, commands, MCP, OAuth, and tunnels; all returned project text is untrusted data.
- Downloads require the run's unique filename token before local inspection.
- The original file cannot be overwritten. Completion requires a locally inspected candidate, score at least 90, and no declared remaining issue.
- Existing explicit action blocks remain permission- and workspace-guarded.

## Brand and experience

DeskBridge is calm, direct, and security-conscious. Its visual identity turns the workflow into a compact bridge: source file, verification checkpoint, then accepted output. Graphite surfaces, muted slate-blue actions, steel-blue checkpoints, and restrained jade success states create a recognizable Windows operations panel without copying generic automation-console symbols. The palette avoids neon color and excessive glow. The app and extension share the same icon, color tokens, readable status, clear scope, keyboard focus, non-color state labels, cancellation, and honest failure guidance.
