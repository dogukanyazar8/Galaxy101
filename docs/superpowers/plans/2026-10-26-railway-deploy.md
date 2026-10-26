# Railway Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Publish Galaxy 101's existing server to Railway and document how the Windows client connects.
**Architecture:** One persistent .NET 10 Docker service and one PostgreSQL service. Keep the current server, shared rules and protocol; configure Railway directly rather than adding infrastructure frameworks.
**Tech Stack:** ASP.NET Core 10, PostgreSQL, Docker, GitHub, Railway CLI.
**Spec:** `docs/deployment-target.md`; user's 26 October Railway deployment request.

## Global Constraints

- Preserve the main checkout's unrelated, uncommitted v4 UI work; deploy from the isolated `codex/railway-deploy` branch.
- Repository: `dogukanyazar8/Galaxy101`; Docker context is the repository root, Dockerfile `Server/Dockerfile`.
- One application replica; port8080; PostgreSQL persistent storage; signing key generated privately and retained in Railway variables.
- New Railway services no longer read legacy Config as Code. Apply builder, healthcheck, watch patterns and replica settings directly to the service. Keep `railway.json` only for legacy compatibility.
- No public database port or extra services needed for this first game test. No credentials committed or printed.

## Review Focus

- Missing shared sources: validate the existing Docker image from repository-root context.
- Port/DB mismatch: set PORT8080 and a PostgreSQL variable reference; `/health` must return200.
- Wrong build/version: `/v1/info` must report0.4.0 and101-v4.
- Wrong transport: verify real HTTPS guest authentication and WSS request/ack exchange.
- Restart expectations: document that v4 aborts open matches and refunds net stakes; it does not resume them.

### Task 1: Validate and document the existing deployment
- [x] Build `Server/Dockerfile` locally, inspect production startup, update `Server/README.md` and `docs/RAILWAY.md`. Production Docker smoke passed: health,0.4.0/101-v4, guest authentication,10,000 chips.
- [x] Verify the isolated diff and keep unrelated Unity assets/settings unchanged. Independent read-only deployment review found no blocking issue.

### Task 2: Publish GitHub and provision Railway
- [x] Commit the deployment files and upload the repository with Git LFS to the provided private repo, preserving its initial README commit.
- [x] Create Galaxy101 project, PostgreSQL and API service in the authenticated workspace; configure required references/secrets/port and direct service settings. CLI dot-path no-op worked around with stdin JSON; deployment manifest verified.
- [x] Connect GitHub main, generate HTTPS domain and deploy; inspect build/runtime logs. API deployment `9457d23f-2022-45da-928f-259a9ea9aa3e` succeeded.

### Task 3: Verify the live service and deliver instructions
- [x] Check `/health`, `/v1/info`, guest API and authenticated WSS; record observed results. LiveServer tests:2 passed,0 failed,0 skipped,36s; no Unity process used.
- [x] Update the deployment URL/status and Windows launch instructions; commit documentation and preserve main's original WIP. Independent final documentation review found no blocker. Git fast-forward integration and before/after WIP hashes verify preservation.
