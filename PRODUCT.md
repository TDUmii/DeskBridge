# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Stack

C# and .NET 8 for a Windows WPF control panel, a .NET 8 class library, and a .NET 8 native-messaging console host. The ChatGPT Web integration is a dependency-light Manifest V3 Chrome extension written in TypeScript, HTML, and CSS.

## Users

Windows users who work in ChatGPT Web and want to apply small, explicit file, project, asset, image, application, and development-tool actions to a user-selected local workspace without opening a separate coding session.

## Product Purpose

DeskBridge is a local companion that executes structured actions proposed in ChatGPT responses. ChatGPT performs language understanding and code generation; DeskBridge validates, asks permission, executes locally, and returns a structured result. V1 succeeds when the documented end-to-end demos work on Windows and a second machine can clone, build, install, and run the repository from the README.

## Positioning

DeskBridge is deliberately not an LLM, browser automation agent, or local HTTP service. Its distinguishing mechanism is a narrow, inspectable Chrome Native Messaging bridge whose native host remains the security authority for every action.

## Operating Context

Users select one allowed workspace, ask ChatGPT Web for a task, explicitly click a DeskBridge action block, review any required confirmation in the desktop app, and receive a result in the extension. Common workflows are creating or editing multi-file static websites, managing images and public assets, previewing sites, opening projects, and running safe development commands such as Git.

## Capabilities and Constraints

- Windows 10/11 first; .NET 8, Chrome, Node.js, and Git are required for development.
- Production communication uses Chrome Native Messaging only; no network-listening API.
- No arbitrary shell, PowerShell, CMD, mouse/keyboard automation, credential access, ChatGPT token/cookie access, deletion, shutdown, restart, or private ChatGPT API.
- Webpage output is untrusted. The native host validates the protocol, action whitelist, permission, workspace boundary, and action-specific arguments.
- File/project destinations remain inside the selected workspace. Downloads are HTTPS-only, size-limited, content-validated, and protected against private-network SSRF including redirects.
- Image V1 supports inspection, resize, compression, PNG/JPEG/WebP conversion, and explicit local import. ChatGPT image saving is best-effort and falls back to import.
- UI must expose cancellation and terminal success/failure states; no action runs automatically from page content.

## Brand Commitments

The product name is DeskBridge. Voice is direct, calm, and security-conscious. UI is simple, modern, readable, and native to a Windows utility; status, scope, and consequences take priority over decoration.

## Evidence on Hand

The supplied specification defines the complete repository topology, protocol, supported actions, error codes, permission levels, 16 end-to-end demo flows, test matrix, and Git workflow. No external commercial claims, customer logos, testimonials, or brand assets are supplied and none may be fabricated.

## Product Principles

- Safety before convenience.
- Explicit user action and visible permission before side effects.
- Native validation is the source of truth.
- Small, debuggable components instead of giant dispatch code.
- Working end-to-end flows and honest limitations over placeholders.

## Accessibility & Inclusion

The desktop and extension interfaces require keyboard focus visibility, readable contrast, clear non-color status labels, descriptive action copy, and useful loading, empty, disabled, success, and error states.
