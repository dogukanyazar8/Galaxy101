# Local Reliability Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close the reported correctness and lifecycle defects, verify locally, and commit all reviewed local source work without publishing or deploying.
**Architecture:** Keep the Unity client, shared rules and single .NET/PostgreSQL server. Persist account session versions and operation/settlement identity in existing entities. Keep the current actor; fix ownership and transaction boundaries rather than introduce new infrastructure.
**Tech Stack:** Unity 6000.6.6f1, C# 9 client, .NET 10 server, EF Core, PostgreSQL, NUnit/xUnit, existing Python/PowerShell tools.
**Spec:** User's request to fix all findings from the preceding audit and commit local work; evidence in `artifacts/recheck-20261026T131228/summary.json` and regression sources.

## Global Constraints

- Use `C:/GD/Okey 101`, main at d579efd plus the existing 10 modified and 5 untracked files. Preserve and review that work; user explicitly authorized committing it.
- Railway remains suspended. No push, deployment or live database mutation.
- Test/build/capture processes run sequentially. Never run concurrent Unity processes or full-screen captures. Use existing process mutex and bounded capture guard.
- Never stage secrets, generated assemblies, captures, test logs or temporary review probes.
- Root owns shared EF entity/model/migration edits, integration, test scheduling and commits; independent workers own auth, financial runtime and client lifecycle.
- Preserve existing public behavior unless required to close a defect. Reject old JWTs without session versions deliberately; new login/refresh uses the current durable version.

## Review Focus

- Same-second password reset, pre-reset WebSocket tickets, restart and reset/refresh races.
- Commit succeeded but response lost: leave, settlement, XP and pending stake refunds must remain exactly once or recoverable.
- Old HTTP/WS callbacks after account change or scene disposal must not mutate the new owner.
- Pair openings and pair finishes at 9/10-pair thresholds must use the real available pairs despite rack layout capacity.
- Session replacement and loading cancellation must release/retain server ownership intentionally; no implicit takeover.
- Blocked spectators, offline completed participants and legacy replacement seats must be handled consistently.

## Rulings

- Work in the original checkout: the requested local work exists there, while the attached Railway checkout is stale. Creating another copy would hide the changes the user asked to commit.
- Use one combined additive EF migration for Account.SessionVersion, MatchParticipant.OperationKey and MatchRecord.SettlementJson; validate fresh migration and existing-data defaults.
- No claim of zero future defects. Require focused regression, broad relevant tests, independent review and explicit runtime limits.

## Tasks

### 1. Durable session ownership
- [x] Run auth regressions red, persist/validate session version in JWT and tickets, serialize reset/issue/refresh and verify green.
- [x] Add combined migration and verify upgrades/defaults; retain secure failure behavior.

### 2. Durable financial and game completion
- [x] Run regressions red for repeated leave, bonus XP, settlement receipt, pending refund and block delivery.
- [x] Add stable participation operation identity, transactional bonus XP and persisted settlement receipt; prevent completion while refunds are owed.
- [x] Clean up idle participants and blocked spectator delivery; verify game, economy, ranked and legacy interactions.

### 3. Client ownership and navigation
- [x] Run delayed-account response regression red; make API/session callbacks generation-bound and connection establishment single-flight.
- [x] Handle replaced sessions and cancelled loads without stealing sockets or retaining seats; verify leave/reconnect errors.
- [x] Add focused lifecycle tests and compile all Unity assemblies before bounded native checks.

### 4. Rack, legacy and existing local work
- [x] Reproduce high pair-threshold/finish failures; preserve usable pairs independently of placement capacity, with regression coverage.
- [x] Fix legacy duplicate-seat identity and verify replacement settlement paths.
- [x] Review existing HUD, diagnostics/build/capture scripts, comparison masks and native UI rendering evidence. Correct actionable application defects without hiding unexpected engine errors.

### 5. Integration and delivery
- [x] Run relevant tests in sequential bounded groups, check migration, build and supported native flows.
- [x] Independent spec/quality review; address findings, update verification/status notes.
- [x] Stage reviewed source and existing local work; commit coherent changes; verify clean status and no accidental publication.

## Observed Verification (local, 26 October)

- Auth RED reproduced, then 7/7 GREEN; review clean. Additive upgrade/default/unique-key regression passed on an owned database; EF reports no pending model change.
- Durability/block/leaver regressions observed RED; final combined 27/27 GREEN includes auth and legacy-seat regression. Existing server classes passed in nine sequential groups (159 cases); three older audit cases and one migration case passed separately. Distinct server total: 189.
- Packed-pair RED reproduced; pure table/rack suite 46/46 GREEN.
- Native client final 17/17 GREEN includes delayed-owner, terminal dispose, replaced notification, missed-start snapshot, JSON-null seat, hello recovery and pending-die command/affordance race. Table/HUD/exit and strict lobby group 16/16 GREEN with graphics.
- Real local Production API + owned PostgreSQL + authored Unity scene: all three acceptance flows passed: private waiting room/start/HUD leave, three pairs/katlama options, natural one-hand bot match and lobby result (last run: 16 human actions, 5 draws, 5 discards). No remote services used.
- NoGraphics is suitable for transport/model tests. The authored URP table needs graphics; Null-device attempts failed with render texture errors and were rerun with graphics. No error ignore was added; previous lobby ignore was removed.
- Capture guards 5/5 and comparison masks 2/2 passed; changed PowerShell scripts parse cleanly.
- Final native client 17/17 and natural live match passed again after the in-flight guard. No push/deploy.
- v4 engine/vector/parity groups passed 14/110/72 respectively. Server checkpoint commit: 72c3c50; packed-pair checkpoint: 661a34e. Final client double-click regression observed RED, then the complete native client group passed 17/17 with the synchronous in-flight guard. Independent final review found no concrete P1/P2 regression.
- All reviewed local source work is committed in coherent server, rack, client and validation groups. Final delivery checks include an empty working-tree status, clean staged/unstaged whitespace checks and no owned test processes or containers left running. The existing user development database container is retained.
