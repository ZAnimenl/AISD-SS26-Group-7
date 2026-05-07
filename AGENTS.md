# AGENTS.md

This file is the global orchestration guide for coding agents working in this repository.

It defines:

1. Which specification documents are authoritative.
2. How agents should route work to the correct module.
3. Which skills should be used for planning, coding, review, and handoff.
4. Global coding and project rules that apply to all work.

---

# 1. Authoritative Documents and Priority

Before planning or modifying code, inspect the relevant specification documents.

Follow this priority order when documents or implementation choices conflict:

1. `SPEC.md`
   - Main product/system requirements specification.
   - Defines goals, non-goals, stakeholders, user stories, REQ/NFR statements, constraints, MVP clarifications, and acceptance criteria.

2. `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
   - Architecture and module-boundary specification.
   - Defines the four-module architecture and security boundaries.
   - Some endpoint/schema examples are older. For current assessment attempt, workspace, run, submit, and AI API routes, follow `SPEC.md` and `complete_frontend_api_list_and_backend_alignment.md`.

3. `complete_frontend_api_list_and_backend_alignment.md`
   - Frontend/backend API contract and integration alignment document.
   - Defines endpoint names, request/response shapes, status values, error format, MVP API decisions, mock/TODO(API) rules, and frontend-backend boundaries.

4. `ui-style-reference.md`
   - Visual style reference only.
   - It must not override requirements, architecture, authentication behavior, database schema, existing routes, assessment/submission/reporting behavior, or API contracts.


Do not modify these specification documents unless explicitly asked by the user.

If a task conflicts with the specification documents, stop and report the conflict before editing implementation files.

---

# 2. Skills Directory

Specialized reusable agent instructions live under:

```text
.agents/skills/
```

Use these skills instead of writing a large custom prompt from scratch.

Available skills:

```text
.agents/skills/spec-guardian/SKILL.md
.agents/skills/module-router/SKILL.md
.agents/skills/prompt-commander/SKILL.md
.agents/skills/module1-identity-assessment-coder/SKILL.md
.agents/skills/module2-frontend-ide-coder/SKILL.md
.agents/skills/module3-sandbox-execution-coder/SKILL.md
.agents/skills/module4-ai-telemetry-coder/SKILL.md
.agents/skills/fullstack-integration-coder/SKILL.md
.agents/skills/strict-code-reviewer/SKILL.md
.agents/skills/handoff-summary/SKILL.md
```

When the user asks for planning, coding, review, or handoff, prefer using the matching skill.

## 2.1 Skill Chaining

Skills can and should point to companion skills when a task crosses boundaries.

Use one primary skill for ownership, then name supporting skills explicitly in the prompt when they are needed.

A companion skill provides boundary checks and contract awareness. It does not expand the implementation scope unless the commander explicitly marks it as primary or approves cross-module work. If the task truly requires edits across modules, use `fullstack-integration-coder` as the primary skill or get explicit commander approval for cross-module implementation.

Common chains:

- Planning a coding task: `prompt-commander` -> module coder -> `strict-code-reviewer`
- Unclear ownership: `module-router` -> chosen module coder -> `strict-code-reviewer`
- Frontend with backend API contract risk: `module2-frontend-ide-coder` + `fullstack-integration-coder` -> `strict-code-reviewer`
- Backend API work with frontend consumers: `module1-identity-assessment-coder` + `fullstack-integration-coder` -> `strict-code-reviewer`
- Sandbox execution changes: `module3-sandbox-execution-coder` -> `strict-code-reviewer`
- AI backend changes: `module4-ai-telemetry-coder` -> `strict-code-reviewer`
- Context handoff: active coder/reviewer -> `handoff-summary`

When producing a prompt for another agent, include a short `Skills to use` section that lists the primary skill and any companion skills in order.

---

## 2.2 MCP Server Usage

MCP servers are external tools configured in the user's coding-agent environment. They are used during phases that need live documentation, external review context, browser verification, or database/schema inspection. They are not project runtime dependencies and should not be committed with private credentials.

MCP workflow guidance lives in:

```text
.agents/mcp-usage.md
```

Use MCP servers as live context or tool access. They do not override `SPEC.md`, the architecture PDF, API alignment docs, module boundaries, security rules, or local skills.

Use by phase:

- Planning/implementation: use `context7` for current library/API documentation.
- Team review/release/handoff: use `github` for issues, pull requests, branches, CI/checks, and review context.
- Frontend implementation/review: use `browser` / `playwright` for local route, form, screenshot, and interaction checks.
- Backend/fullstack integration: use read-only database MCP for schema/data inspection when a database MCP server is configured.
- Requirements sanity check: optionally use the project-provided `mcp-code-analyzer` MCP server to run `scan_requirements_compliance`.

### Project MCP: `mcp-code-analyzer`

This repository may include a project-provided MCP server under:

```text
mcp-code-analyzer/
```

It exposes a `scan_requirements_compliance` tool that reads `SPEC.md` and produces a heuristic requirements-compliance report.

Use this tool only as an advisory sanity check during planning, review, or handoff. Its output does not prove implementation correctness and does not override `SPEC.md`, the architecture PDF, API alignment docs, module boundaries, security rules, or local skills.

If the analyzer output conflicts with authoritative specs or code review findings, follow the authoritative specs and report the conflict.

Do not commit MCP OAuth tokens, API keys, bearer tokens, local MCP config, or personal agent settings.

---

# 3. Agent Workflow

## 3.0 Default Implementation Workflow

For every implementation request, use this strict sequence unless the user explicitly asks for planning-only or review-only work:

1. Read `AGENTS.md`.
2. Use `prompt-commander` first to route the task, define scope, identify risks, and choose the skill chain.
3. Run the skill activation checklist in section 3.0.1 and state which skills are active, which are inactive, and why.
4. Implement with the selected primary coding skill.
5. Use companion skills only for the specific boundary or contract risks identified by `prompt-commander`.
6. Finish with `strict-code-reviewer`, or provide the exact reviewer checklist if a separate review pass will run next.
7. Report the closed implementation loop from section 3.4.

Do not skip `prompt-commander` for implementation tasks. For tiny mechanical edits, the commander pass may be brief, but it must still identify the owning module and selected skill chain before editing.

The same agent may continue from the commander pass into implementation. Do not stop after producing a refined prompt unless the user asked for planning-only or prompt-generation-only work.

For planning-only work, use `prompt-commander` and do not modify files.
For review-only work, use `strict-code-reviewer` and do not modify files.
For handoff-only work, use `handoff-summary` and do not modify files unless explicitly asked.

Default implementation prompt users may give:

```text
Read AGENTS.md and follow the default implementation workflow with maximum strictness.
Use prompt-commander first, run the skill activation checklist, then implement with the selected skill chain.

Task: <describe the implementation>

Do not edit specification documents. Respect module boundaries. After implementation, run relevant checks and report changed files, verification commands, spec/API/security risks, and review status.
```

## 3.0.1 Skill Activation Checklist

Before implementation, consider every local skill and activate only the ones that apply:

1. `spec-guardian`
   - Activate when the task touches requirements, acceptance criteria, security boundaries, hidden tests, session/attempt behavior, AI-provider safety, or any possible conflict between specs and implementation.
   - If inactive, state that no spec conflict or requirements interpretation risk was identified.

2. `module-router`
   - Activate when module ownership is unclear after the initial commander pass.
   - If inactive, state that ownership was clear and name the owning module.

3. `prompt-commander`
   - Always activate first for implementation tasks.
   - Use it to choose the primary coding skill, companion skills, forbidden scope, and verification expectations.

4. `module1-identity-assessment-coder`
   - Activate for backend identity, auth/RBAC, assessment, question/test-case persistence, attempts, workspace persistence, submissions, results, reports, EF Core, PostgreSQL, or backend API ownership.

5. `module2-frontend-ide-coder`
   - Activate for Next.js UI, browser IDE, Monaco/editor, student/admin pages, frontend API client behavior, loading/error states, UI polish, mock data, or frontend workspace behavior.

6. `module3-sandbox-execution-coder`
   - Activate for sandboxed execution, grading, hidden test evaluation, stdout/stderr capture, workers/queues, resource limits, timeouts, or execution result schema.

7. `module4-ai-telemetry-coder`
   - Activate for AI backend service, provider integration, secure AI proxy, telemetry, semantic tags, structured AI responses, inline completion backend, system prompts, or AI provider error handling.

8. `fullstack-integration-coder`
   - Activate as primary when the task requires coordinated frontend/backend implementation.
   - Activate as companion when a module-specific task has API, auth, session/attempt, request/response, or end-to-end contract risk.

9. `strict-code-reviewer`
   - Activate after implementation unless the user explicitly says a separate reviewer will handle review.

10. `handoff-summary`
    - Activate when context is getting full, work is paused midstream, or the user asks for a handoff.

Skill activation is a checklist, not permission to use every skill at once. A skill that is inactive should be briefly marked inactive with a reason. This prevents forgotten skills while keeping ownership narrow.

## 3.0.2 Hallucination-Reduction Rules

To reduce ambiguity and repeated-instruction drift:

1. Treat `AGENTS.md` as the single global orchestration source.
2. Treat skill files as role-specific instructions, not separate sources of project truth.
3. If a skill repeats a global rule, use the `AGENTS.md` version when wording differs.
4. Do not infer requirements from skill wording when the authoritative specs say something different.
5. Do not invent missing API contracts, routes, schema fields, hidden-test behavior, or AI-provider behavior.
6. When uncertain, inspect local code and specs before acting; if still uncertain, report the uncertainty instead of guessing.
7. Keep implementation scope tied to the active primary skill. Companion skills provide checks and constraints, not extra permission to expand scope.

## 3.1 Planning / Commander Work

Use this skill first for every implementation task, and also when the user asks for planning, phase breakdown, task routing, or coding prompts:

```text
Use the prompt-commander skill.
```

The commander should:

1. Inspect the repository and relevant specs.
2. Decide which architecture module owns the task.
3. Define allowed and forbidden scope.
4. Keep the implementation prompt as one focused stage by default.
5. Split work into phases only when the user asks for phases or the task is too risky to implement in one pass.
6. Include a `Skills to use` section in generated prompts, with primary and companion skills.
7. Run the skill activation checklist from section 3.0.1.
8. Produce the exact coding-agent prompt for the requested stage.
9. Produce a review checklist.
10. Require implementation output to include verification commands, changed files, contract/spec risks, and review status.

The commander must not modify files unless explicitly instructed.

---

## 3.2 Module Routing

If it is unclear which module owns a task, use:

```text
Use the module-router skill.
```

The router should classify the task into one of:

1. Module 1 - Identity and Assessment Management
2. Module 2 - Interactive Browser-Based Workspace / Frontend IDE
3. Module 3 - Sandboxed Code Execution and Evaluation Engine
4. Module 4 - AI Telemetry and Assistance Service
5. Cross-module integration

The router must not modify files.

---

## 3.3 Coding Work

Use exactly one primary coding skill unless the task is explicitly cross-module. Companion skills may be active for boundary checks, but they do not expand the write scope unless `prompt-commander` explicitly approved cross-module implementation.

### Module 1: Identity / Assessment / Backend / Database

Use:

```text
Use the module1-identity-assessment-coder skill.
```

Use for:

- authentication
- authorization / RBAC
- users and roles
- assessment management backend
- questions and test cases persistence
- assessment attempts
- autosave persistence
- submissions
- results
- reports
- EF Core
- PostgreSQL
- backend API contracts

---

### Module 2: Frontend / Browser IDE / UI

Use:

```text
Use the module2-frontend-ide-coder skill.
```

Use for:

- Next.js frontend
- browser IDE
- Monaco/editor
- student dashboard
- admin dashboard
- student/admin pages
- frontend API clients
- frontend loading/error/empty states
- UI polish
- mock data
- TODO(API) comments

---

### Module 3: Sandboxed Execution / Evaluation

Use:

```text
Use the module3-sandbox-execution-coder skill.
```

Use for:

- sandboxed execution
- grading engine
- hidden test evaluation
- stdout/stderr capture
- execution workers
- queues
- resource limits
- timeout/memory handling
- execution result schema

---

### Module 4: AI Telemetry / AI Assistance

Use:

```text
Use the module4-ai-telemetry-coder skill.
```

Use for:

- AI backend service
- secure AI proxy
- LLM provider integration
- AI interaction logging
- token usage telemetry
- semantic tags
- structured AI responses
- inline completion backend
- AI provider error handling

---

### Cross-Module Integration

Use:

```text
Use the fullstack-integration-coder skill.
```

Use for:

- frontend/backend API integration
- auth flow integration
- JWT/cookie/token behavior
- frontend/backend request/response mismatch
- dashboard data loading from backend
- assessment/attempt/workspace API flow
- end-to-end integration fixes
- integration tests

Before coding, the agent must state:

1. Which modules are involved.
2. Which files are likely affected.
3. Why cross-module work is necessary.

---

## 3.4 Closed Implementation Loop

Every implementation task must end with:

1. Changed files.
2. Verification commands run, or a clear reason each relevant command could not be run.
3. API/spec/security risks checked.
4. Remaining TODOs or open decisions.
5. Review status:
   - run `strict-code-reviewer`, or
   - provide the exact reviewer prompt/checklist if a separate review agent will run next.

Do not treat implementation as complete until this loop is reported.

---

## 3.5 Review Work

Use:

```text
Use the strict-code-reviewer skill.
```

The reviewer must:

1. Review latest changes only.
2. Not modify files.
3. Inspect `git status` and relevant diffs.
4. Run available checks when possible.
5. Report findings by severity:
   - P0: blocker/data loss/security disaster
   - P1: must fix before merge
   - P2: should fix soon
   - P3: minor but worth noting

Focus on:

- spec compliance
- module ownership
- API contract consistency
- auth/RBAC
- hidden-test protection
- sandbox safety
- AI-provider safety
- frontend/backend integration
- build/test failures

---

## 3.6 Handoff Work

Use:

```text
Use the handoff-summary skill.
```

Use when a Coding Agent session context window is getting full.

The handoff must include:

1. Current goal
2. Current session role
3. Module(s) involved
4. Specs already read
5. Files changed
6. Commands run
7. Known bugs/TODOs/open questions
8. Next recommended prompt
9. What the next session must not change

---

# 4. Four-Module Architecture

The project follows a four-module architecture.

## Module 1 - Identity and Assessment Management

Owns:

- authentication
- RBAC
- users and roles
- assessments
- questions and test cases
- attempt/session lifecycle
- workspace persistence
- submissions
- results
- reports
- database persistence

Must protect:

- hidden test cases
- expected hidden outputs
- user identity
- role permissions
- scores
- assessment attempt state

---

## Module 2 - Interactive Browser-Based Workspace / Frontend IDE

Owns:

- frontend UI
- browser IDE
- Monaco/editor
- student/admin pages
- problem statement display
- language selector
- timer display
- autosave indicator
- run/submit UI
- output console
- AI assistant UI
- frontend API clients

Must not:

- access database directly
- call sandbox directly
- call external LLM APIs directly
- execute student code locally
- expose hidden test cases in student UI
- create/store/trust/send a real frontend-managed `session_id`

---

## Module 3 - Sandboxed Code Execution and Evaluation Engine

Owns:

- isolated execution of untrusted code
- resource limits
- stdout/stderr capture
- hidden test evaluation
- execution result schema
- worker/queue execution lifecycle
- cleanup of execution environments

Must not:

- expose hidden test details to student APIs
- be directly called by frontend
- run untrusted code in the normal web backend process
- weaken isolation for convenience

---

## Module 4 - AI Telemetry and Assistance Service

Owns:

- secure AI proxy
- external LLM provider calls
- server-side system prompts
- AI interaction logging
- token usage telemetry
- semantic tagging
- structured AI responses
- AI provider error handling

Must not:

- expose provider API keys to frontend
- let frontend call external LLM APIs directly
- leak system prompts
- leak hidden tests or grading criteria
- bypass logging/rate-limit controls where required

---

# 5. Global Engineering Rules

1. Use English for code comments.
2. Do not use emojis in code comments or commit messages.
3. Do not break the existing project structure unless explicitly approved.
4. Add tests incrementally where appropriate.
5. Keep responsibilities separated:
   - each class should have one responsibility
   - each method should do one clear thing
6. Prefer small, reviewable changes.
7. Do not commit secrets, credentials, API keys, JWT secrets, or local environment files.
8. Do not change the project stack unless explicitly approved.
9. If the frontend is Next.js, do not convert it to Vite.
10. If the frontend is Next.js, do not add `react-router-dom`.
11. Do not invent new API contracts if an existing spec or backend route exists.
12. If implementation requires changing an API contract, report the reason first.

---

# 6. Database and EF Core Rules

The project uses PostgreSQL.

Local development connection string may be:

```text
Host=localhost:5433;Database=ai_coding;Username=ai_coding;password=password
```

Do not hardcode production credentials.

When configuring EF Core classes with Fluent API:

1. Create a single static `XxConfiguration` class.
2. Implement a static `Configure(ModelBuilder modelBuilder)` method.
3. Call this `Configure` method from `OnModelCreating` in the `DbContext`.

Do not add migrations unless the task explicitly requires database schema changes.

---

# 7. Frontend Rules

1. Use English for code comments.
2. Do not use emojis in UI text unless explicitly approved.
3. Do not use emojis in comments or commit messages.
4. Do not use classical blue gradient styling.
5. Prefer flat, clean UI unless the current approved UI spec says otherwise.
6. Border radius should generally be no more than 4px unless the approved UI style document or existing design system requires otherwise.
7. Put page UI into separate files/components where practical.
8. Support localization if the current frontend architecture already supports it or the task explicitly asks for it.
9. If localization is implemented:
   - support English, Chinese, and German
   - default language should be English
   - provide language switcher in the top-right area where appropriate
   - store language preference in local storage
10. Do not let visual style override product requirements, route behavior, auth behavior, database schema, or API contracts.

---

# 8. Session and Attempt Rule

The architecture PDF may contain older schema examples using `session_id`.

For the current implementation, follow the newer requirements/API alignment decision:

1. Frontend must not create, store, trust, or send a real `session_id`.
2. Backend should identify the user from authentication context, such as JWT or another secure token.
3. Backend should resolve the active assessment attempt from authenticated user + `assessment_id`.
4. Frontend mock state is allowed only for visual/frontend-only MVP behavior.
5. Public backend-connected assessment flows are assessment-scoped. Frontend workspace, run, submit, and AI calls must not send a `session_id` or `attempt_id`; the backend resolves the active attempt internally.

If a task appears to require frontend-managed `session_id`, stop and ask for clarification.

---

# 9. Hidden Test Case Rule

Student-facing UI and student-facing APIs must never expose:

- hidden test case input
- hidden expected output
- grading implementation
- admin-only notes

Student-facing UI may show hidden test summary counts only if allowed by the specs.

Admin-facing UI may show hidden test metadata/details only where the role and route are authorized.

---

# 10. Untrusted Code Execution Rule

Do not execute student code through:

- frontend JavaScript
- `eval`
- `child_process`
- normal backend request handlers
- unrestricted local Python/Node/.NET runtimes

Untrusted code execution belongs to Module 3 and must follow the sandbox architecture.

---

# 11. AI Provider Rule

Frontend must not call external AI providers directly.

AI provider calls belong to Module 4 and must protect:

- provider API keys
- server-side system prompts
- hidden tests
- grading criteria
- user/assessment context
- telemetry logging

For frontend-only work, use mock AI responses unless explicitly instructed otherwise.
