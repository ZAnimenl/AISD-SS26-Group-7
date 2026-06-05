---
name: shared-prototype-assessment-coder
description: Use this for work on the SPEC-defined shared runnable prototype assessment: four task categories, platform-native starter files, task-specific preview/verification modes, seeded tasks/test cases, and conversion of external prototypes into assessment content.
---

# Shared Prototype Assessment Coder Skill

You are the coding agent for the shared-prototype assessment flow described by `SPEC.md`.

Use this skill when the task involves:

- creating or updating the four-task assessment model
- seeding or editing practical task content
- converting an external prototype into platform-native starter files
- configuring task categories, verification modes, starter files, or test cases
- implementing the UI preview/verification area for task types
- ensuring students can run tasks inside the website without local dependency installation
- adding administrator support for LLM-generated task/test drafts

## Core Interpretation

The shared runnable prototype is platform-managed assessment starter content. It does not mean students must clone, install, or run a full external project locally.

When adapting a full prototype, distill it into:

- persisted starter files
- task descriptions
- supported language metadata
- task type and difficulty metadata
- verification mode metadata
- public and hidden test cases
- optional preview harness content for frontend UI extension tasks

## Four Required Task Categories

For the first implementation, an assessment should contain one focused task from each category:

1. Frontend UI extension
2. REST API development
3. Database query/schema work
4. Bug fixing in existing code

Each task should be small enough to complete inside the browser workspace with AI assistance and automated verification.

## Module Ownership

This skill is cross-module by nature. Keep ownership explicit:

- Module 1 owns assessment, task, test-case, starter-code, prototype-reference, verification-mode, submission, and report persistence.
- Module 2 owns workspace display, file browser/editor behavior, task switching, run/submit controls, and preview/verification UI.
- Module 3 owns sandboxed execution, grading, public/hidden test execution, stdout/stderr, and result safety.
- Module 4 owns embedded AI agent behavior, AI interaction logging, token tracking, token efficiency, and LLM-assisted task/test draft generation.

Use `fullstack-integration-coder` as a companion or primary skill when implementation spans frontend/backend contracts.

## Preview and Verification Rules

- Frontend UI extension tasks shall provide a direct browser UI preview.
- REST API development tasks may use endpoint request/response output, API tests, or automated test output.
- Database query/schema tasks may use query result tables, schema validation, or database-oriented tests.
- Bug-fixing tasks may use whichever preview or regression output matches the defect.

Do not require a browser preview for every task type.

## Student No-Install Rule

Students must be able to complete assessment tasks in the website without installing dependencies locally.

Do not design task flows that require students to:

- run `npm install`
- run `pip install`
- start local Vite/FastAPI/Next/.NET services
- configure local databases
- manually open local ports

Dependency installation, runtime setup, and sandbox execution belong to platform infrastructure, not the student assessment workflow.

## Security Rules

Student-facing UI and APIs must not expose:

- hidden test case input
- hidden expected output
- grading implementation
- admin-only notes
- provider API keys
- system prompts

Untrusted student code must not run in the frontend, normal backend request handlers, or unrestricted local runtimes.

## LLM-Assisted Task Authoring

LLM-generated tasks and tests are drafts only.

Administrators must be able to review, edit, approve, or discard generated drafts before publication. Persist provenance where practical: manual, LLM-generated, or administrator-edited after generation.

## Required Workflow Before Coding

1. Read `SPEC.md`, especially REQ-11a through REQ-18j and REQ-30a through REQ-30f.
2. Inspect current assessment/task/test-case data models and seeders.
3. Inspect current workspace/run/submit/preview UI.
4. Identify which modules are affected.
5. State which files are likely affected and why.
6. Confirm that students will not need local dependency installation.
7. Then implement only the requested shared-prototype assessment scope.

## Required Workflow After Coding

1. Run relevant frontend checks if UI changed.
2. Run relevant backend build/tests if backend changed.
3. Run execution/grading checks if Module 3 changed.
4. Report changed files.
5. Report task categories and verification modes touched.
6. Confirm hidden-test protection.
7. Confirm no-install student workflow.
8. Finish with review status: run `strict-code-reviewer` or provide its checklist for a separate review pass.
