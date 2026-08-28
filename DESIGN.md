# DeskBridge Design System

## Direction

DeskBridge uses a quiet Windows operations-console language: a warm off-white canvas, ink text, restrained blue for primary actions, teal for connected/success states, amber for pending permission, and red only for denial/failure. The interface should feel trustworthy in long desktop sessions and remain legible at 100–150% scaling.

## Mode

Operate. Fast scanning, explicit scope, predictable controls, and durable terminal states outrank visual spectacle.

## Typography

Use Segoe UI Variable or Segoe UI in WPF and the Chrome/system UI stack in the extension. Use monospace only for paths, commands, IDs, and protocol results. Titles are sentence case and compact.

## Layout

The WPF shell has a stable left navigation rail and one primary content surface. Workspace identity and connection state remain visible. Activity is a table/list, not a grid of decorative cards. Permission rows pair the capability with its current policy and a direct control.

The extension popup is narrow and task-focused. Content-script controls attach to the relevant code block or image and do not compete with ChatGPT's own controls.

## Components and states

Buttons have normal, hover, pressed, keyboard-focus, and disabled states. Status always includes text in addition to color. Empty activity and asset views explain what will appear and how to create it. Permission prompts show the action, target, affected files or command, workspace, and Allow once/Cancel choices.

## Motion

Motion is limited to short state transitions and a subtle connected-status pulse. Respect reduced-motion preferences in the extension. No looping decorative animation.

## Accessibility

Text contrast targets WCAG AA. Keyboard navigation follows visual order. Focus rings are never removed. Controls use action-specific labels and errors include recovery guidance.
