# Galaxy HTML fidelity implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Independent asset and animation work may run in parallel; integration and Unity verification are sequential.

**Goal:** Carry the current playable Galaxy 101 HTML design into the existing native Unity game, beginning with the main screen and preserving real services.

**Architecture:** Reuse UI Toolkit screens, the shared settings store and the native background compositor. Source geometry comes from the supplied Three.js exporter; flight simulation and planet transitions remain native C#. Browser wrappers and demo backend behavior are not production implementations.

**Tech Stack:** Unity 6000.6.6f1, UI Toolkit, URP 17.6.0, glTFast 6.20.0, NUnit.

**Spec:** `Design/Galaxy101_ClaudeCode/prototip/Galaxy 101 - Gerçek Ana Oyun.dc.html`, its loaded G101 pages/modules and actual button navigation. `START.md` describes the portable source. Handoff manifests are secondary where stale.

## Global constraints

- Preserve all existing WIP; no reset, stash, commit, push, deployment or live account changes.
- Use the existing game screens, online authority, localization, display and settings mechanisms.
- Preserve source geometry, camera, timeline, orbit seeds, theme response, reduced motion and cleanup.
- Keep deterministic screenshots separate from live animation and gameplay verification.
- Do not interfere with other Unity projects or test processes.

## Review focus

- Rapid theme changes replace an unfinished transition without retaining ghost planets or resources.
- Disabling motion or hiding a screen pauses effects without a resume jump.
- Menu, lobby, auth and table compositions must retain their correct independent placement.
- A missing visual asset must report an actionable error and not break input or the game scene.
- Existing source changes may arrive concurrently; verify touched files before integration.

## Tasks

- [x] Inventory current HTML screens, state routes, settings, animation modules and Unity counterparts; record source conflicts.
- [x] Add native planet-change animation from `G101-Menu.dc.html:1501`; test time boundaries, replacement, reduced motion, render and cleanup.
- [x] Export the five original ship models; implement native deterministic flight/occlusion and lifecycle from `ships3d.js`; integrate menu/table layers with existing background settings.
- [x] Reconcile actual settings navigation and material UI differences discovered by screen comparison; add focused behavior tests.
- [x] Run focused EditMode/PlayMode tests, build a fresh diagnostic player, compare native/reference screenshots at matching state and viewport; report exact remaining gaps.

## Execution decisions

- User explicitly requested autonomous work and no questions. Existing dirty checkout contains the new reference and active UI work, so work in place with bounded file ownership.
- `prototip` and `full project` common design files differ only in dependency URLs; use portable `prototip` and verify the supplied localhost page.
- `scripts/Test-Unity.ps1` initially blocked because its process-name guard sees Unity Hub's `resources/unity.exe serve`. Preserve unrelated processes; distinguish actual project locks when choosing the verification invocation.

- Display footer verified against the served HTML: only Cancel/Save are rendered; the unused applyDisp function does not authorize an extra Apply button. Native flow uses Save and the exact dirty-state note.
- App and build metadata now use the reference version 0.5.0; protocol/rule versions are unchanged.
