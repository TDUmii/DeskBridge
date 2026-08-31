# DeskBridge Design System

## Direction

DeskBridge uses a focused Windows operations-console language with its own bridge-and-checkpoint identity. The mark shows a source document entering a slate-blue route, passing a steel-blue verification point, and becoming a jade accepted output. Light mode uses a cool gray canvas; dark mode uses layered graphite surfaces rather than pure black. Muted slate blue marks primary actions, steel blue marks verification, jade marks connected or successful states, amber marks waiting, and muted rose is reserved for failure. Neon colors and glow effects are excluded.

## Mode

Operate. Fast scanning, explicit scope, predictable controls, and durable terminal states outrank visual spectacle.

## Typography

Use Segoe UI Variable or Segoe UI in WPF and the Chrome/system stack in the extension. Monospace is limited to paths, commands, IDs, and protocol results. Titles use sentence case.

## Layout

The Agent tab presents one linear job: choose create-new, protected workspace-context, or improve-file, provide the requested outcome, review the locked ChatGPT Web transport and GPT-5.6 Sol High 3/3 indicators, then start or cancel and follow status evidence. Create-new visibly states that the workspace is an output boundary and no files are uploaded. Workspace-context states its selective read-only access and blocked secret/write boundary before the run starts. Numbered markers make the setup order scannable, while the current-run card and timeline remain visible beside it at the default window size. The extension uses the same icon and color tokens, plus a compact floating status strip only on the dedicated agent tab; it must not obscure the composer.

The installer is one focused dark-mode surface in the same graphite, slate-blue, steel-blue, and jade system. The left side explains the one-button installation and its live progress. The right side states the privacy boundary and honestly separates automatic Windows setup from Chrome's required visible extension confirmation. The primary action remains in the lower-right corner and becomes a close action only after a durable success or failure state.

## Components and states

Create-new starts with a workspace and non-empty request. Workspace-context additionally allows bounded native-filtered context rounds without a source selection. Improve-file requires one valid source inside that workspace. No API key field exists. Status explicitly names Waiting, Verifying, Idea ready, Context ready, Uploading, ChatGPT Web pass, Local verification, Revising, Completed, Review needed, Cancelled, or Failed. A web failure always states that no Codex/API fallback ran.

Installer states are Ready, Installing, Installed, and Needs attention. Progress copy names the current local operation. Installed copy gives the exact remaining Chrome action and states that the extension path is already on the clipboard. Failure copy names the problem without claiming a rollback outside the install boundary.

## Theme

Both WPF and extension surfaces support light and dark appearance. Text and controls target WCAG AA contrast. Focus rings remain visible. The floating web status strip uses translucent theme-appropriate surfaces and a text label in addition to its status dot.

## Motion

Motion is limited to short state transitions and a subtle working pulse. Reduced-motion preferences disable the pulse.
