# Security model

ChatGPT page content and generated files are untrusted. The native host and local verifier remain the security boundary.

## Web-only boundary

- DeskBridge uses only the signed-in ChatGPT Web interface in a dedicated marked Chrome tab.
- It never calls the OpenAI Platform API, switches to Codex, creates a Codex task/workspace, or stores an API key.
- It does not read cookies, session tokens, local browser profiles, or private ChatGPT endpoints.
- If GPT-5.6 Sol and High (3/3) cannot be verified from the visible page, no prompt is sent and the job fails closed.
- Normal ChatGPT tabs cannot claim autonomous jobs.

## Data flow

Starting a run explicitly authorizes sending the selected file copy and request text through ChatGPT Web. File bytes travel from the native host to the extension in bounded chunks and are assigned to the page's visible upload input. Revisions send local inspection summaries back into the same conversation. No unrelated workspace file is uploaded.

## Download trust

Each run generates an unpredictable safety token that must appear in both the declared and downloaded filename. The service worker watches the exact name, the host verifies name equality, token presence, existence, and file timestamp, then copies it into the run directory before inspection. Generated executables or macro-capable documents remain untrusted and require user review.

## Completion and preservation

The original is copied before browser work and is never a candidate destination. Every candidate receives a local hash and format-appropriate inspection. Publication requires score at least 90 and an empty remaining-issues list. Reaching the iteration limit preserves the best version for review without claiming success.

## Existing local actions

Structured action blocks still pass through schema validation, action whitelist, permission policy, normalized workspace boundary, and action-specific validation. Side effects default to Ask. There is no arbitrary PowerShell/CMD action, credential extraction, registry editing action, shutdown/restart, remote control, or destructive filesystem action.

## Network and logs

Native Messaging exposes no listening network port. HTTPS asset downloads retain redirect-by-redirect SSRF protection and size/type validation. Activity logs contain metadata only. Browser credentials, file contents, screenshot bytes, and tokens are excluded from logs.
