# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Stack

C# and .NET 8 for a Windows WPF file-agent control panel, class library, and native-messaging console host. The optional ChatGPT Web integration is a dependency-light Manifest V3 Chrome extension written in TypeScript, HTML, and CSS. Autonomous work uses the official OpenAI Responses API with function tools and Code Interpreter.

## Users

Windows users who want to describe a file outcome in ordinary language and receive a locally inspected result without manually uploading and downloading every revision. The explicit ChatGPT Web action workflow remains available for lightweight local operations.

## Product Purpose

DeskBridge is a bounded autonomous file agent plus a local browser companion. The Agent sends one explicitly selected source file and request to the official OpenAI Responses API, downloads versioned candidates, checks them locally, repeats within budgets, and publishes the best accepted artifact without changing the original. The browser companion executes structured actions proposed in ChatGPT responses through the existing permission boundary.

## Positioning

DeskBridge is deliberately not ChatGPT Web automation or a local HTTP service. It never borrows a browser login. Autonomous behavior uses an explicit Platform API connection; local tools, workspace boundaries, iteration limits, preserved originals, visible evidence, and a completion gate keep the loop inspectable.

## Operating Context

Users select one allowed workspace, save their API key, choose a source file, describe the finished result, and start the Agent with an explicit upload action. They can cancel, observe the run timeline, open evidence, and open the accepted result. The optional browser flow still supports explicit action blocks and permission review.

## Capabilities and Constraints

- Windows 10/11 first; .NET 8, Chrome, Node.js, and Git are required for development.
- Production communication uses Chrome Native Messaging only; no network-listening API.
- No arbitrary shell, PowerShell, CMD, mouse/keyboard automation, credential access, ChatGPT token/cookie access, deletion, shutdown, restart, or private ChatGPT API.
- Webpage output is untrusted. The native host validates the protocol, action whitelist, permission, workspace boundary, and action-specific arguments.
- File/project destinations remain inside the selected workspace. Downloads are HTTPS-only, size-limited, content-validated, and protected against private-network SSRF including redirects.
- Image V1 supports inspection, resize, compression, PNG/JPEG/WebP conversion, and explicit local import. ChatGPT image saving is best-effort and falls back to import.
- UI must expose cancellation and terminal success/failure states; no action runs automatically from page content.
- API keys are DPAPI-encrypted for the current Windows user and excluded from settings, logs, evidence, and source control.
- The Agent is bounded to 2–8 configured passes and 1–64 tool calls, cannot overwrite the original, and retains versioned evidence.

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
