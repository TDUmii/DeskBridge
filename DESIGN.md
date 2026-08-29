# DeskBridge Design System

## Direction

DeskBridge uses a focused Windows operations-console language with its own bridge-and-checkpoint identity. The mark shows a source document entering a blue route, passing a cyan verification point, and becoming a teal accepted output. Light mode uses a cool gray canvas; dark mode uses layered graphite surfaces rather than pure black. Electric blue marks primary actions, cyan marks verification, teal marks connected or successful states, amber marks waiting, and pink-red is reserved for failure.

## Mode

Operate. Fast scanning, explicit scope, predictable controls, and durable terminal states outrank visual spectacle.

## Typography

Use Segoe UI Variable or Segoe UI in WPF and the Chrome/system stack in the extension. Monospace is limited to paths, commands, IDs, and protocol results. Titles use sentence case.

## Layout

The Agent tab presents one linear job: selected source, requested outcome, locked ChatGPT Web transport, locked GPT-5.6 Sol and High 3/3 indicators, explicit start/cancel controls, then status and evidence. Numbered markers make the setup order scannable, while the current-run card and timeline remain visible beside it at the default window size. Workspace identity remains visible. The extension uses the same icon and color tokens, plus a compact floating status strip only on the dedicated agent tab; it must not obscure the composer.

## Components and states

Start is enabled when a workspace, existing source, and non-empty request are ready. No API key field exists. Status explicitly names Waiting, Verifying, Uploading, ChatGPT Web pass, Local verification, Revising, Completed, Review needed, Cancelled, or Failed. A web failure always states that no Codex/API fallback ran.

## Theme

Both WPF and extension surfaces support light and dark appearance. Text and controls target WCAG AA contrast. Focus rings remain visible. The floating web status strip uses translucent theme-appropriate surfaces and a text label in addition to its status dot.

## Motion

Motion is limited to short state transitions and a subtle working pulse. Reduced-motion preferences disable the pulse.
