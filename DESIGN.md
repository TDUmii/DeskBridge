# DeskBridge Design System

## Direction

DeskBridge uses a focused Windows operations-console language with its own bridge-and-checkpoint identity. The mark shows a source document entering a blue route, passing a cyan verification point, and becoming a teal accepted output. Light mode uses a cool gray canvas; dark mode uses layered graphite surfaces rather than pure black. Electric blue marks primary actions, cyan marks verification, teal marks connected or successful states, amber marks waiting, and pink-red is reserved for failure.

## Mode

Operate. Fast scanning, explicit scope, predictable controls, and durable terminal states outrank visual spectacle.

## Typography

Use Segoe UI Variable or Segoe UI in WPF and the Chrome/system stack in the extension. Monospace is limited to paths, commands, IDs, and protocol results. Titles use sentence case.

## Layout

The Agent tab presents one linear job: choose create-new or improve-file, provide the requested outcome, review the locked ChatGPT Web transport and GPT-5.6 Sol High 3/3 indicators, then start or cancel and follow status evidence. Create-new visibly states that the workspace is an output boundary and no files are uploaded. Numbered markers make the setup order scannable, while the current-run card and timeline remain visible beside it at the default window size. The extension uses the same icon and color tokens, plus a compact floating status strip only on the dedicated agent tab; it must not obscure the composer.

## Components and states

Create-new starts with a workspace and non-empty request. Improve-file additionally requires one valid source inside that workspace. No API key field exists. Status explicitly names Waiting, Verifying, Idea ready or Uploading, ChatGPT Web pass, Local verification, Revising, Completed, Review needed, Cancelled, or Failed. A web failure always states that no Codex/API fallback ran.

## Theme

Both WPF and extension surfaces support light and dark appearance. Text and controls target WCAG AA contrast. Focus rings remain visible. The floating web status strip uses translucent theme-appropriate surfaces and a text label in addition to its status dot.

## Motion

Motion is limited to short state transitions and a subtle working pulse. Reduced-motion preferences disable the pulse.
