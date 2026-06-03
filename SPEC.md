# SPEC.md

## Note

This version incorporates tutor feedback from 2026-06-03. Key changes from the original specification:

- Task types changed from algorithmic/leetcode-style questions to practical development tasks (web applications, database tasks, API development).
- AI assistance changed from chat/hint levels to an embedded AI agent in the workspace.
- Added AI token usage and efficiency tracking as a core evaluation metric.
- Removed AI Rescue button feature (tutor rejected).
- Added User Acceptance Testing (UAT) requirements.

Requirement IDs in this version are authoritative for future implementation, tests, and review notes.

---

## 1. Overview

### 1.1 Project

Online AI-Coding Assessment / Interview Platform

### 1.2 Problem Statement

Traditional coding assessments mainly evaluate the final code output using algorithmic puzzles. In an AI-assisted development setting, this is no longer sufficient. The platform must support realistic, practical development tasks with an embedded AI agent, allowing administrators to assess candidate performance, code quality, AI usage behavior, and how efficiently students use AI tokens to complete real-world tasks.

### 1.3 Objective

Build a web-based platform that lets administrators create and manage practical coding assessments (web applications, database tasks, API development), lets students complete them in a browser-based IDE-like environment with an embedded AI agent, executes code safely in a sandbox, tracks AI token usage and efficiency, and produces reports that summarize assessment outcomes and AI interaction patterns.

### 1.4 Scope

This document defines the requirements for the system in a structured, testable form suitable for spec-driven development. It focuses on stakeholder needs, user stories, acceptance criteria, constraints, and verifiable requirement statements.

---

## 2. Terminology

- **Practical task** means a development task that simulates real-world software work, such as building a REST API endpoint, writing database queries, creating a web page component, or fixing a bug in an existing codebase.
- **Embedded AI agent** means an AI assistant integrated directly into the coding workspace that can read the student's code context, suggest edits, answer questions about the task, and assist with debugging. Unlike a separate chat panel, the embedded agent operates within the code editing environment.
- **AI token usage** means the total number of LLM input and output tokens consumed during an assessment attempt.
- **Token efficiency** means the ratio of useful AI-assisted progress to total tokens consumed. Administrators can review whether students used AI strategically or wastefully.
- **Starter project** means a pre-configured project scaffold provided with a practical task, including existing files, dependencies, and structure that students build upon.
- **User Acceptance Test (UAT)** means a structured test performed by real users to verify that the system meets requirements from an end-user perspective.

---

## 3. Goals

- Provide a realistic AI-assisted coding assessment environment using practical development tasks.
- Support browser-based coding for at least Python and JavaScript/TypeScript.
- Allow administrators to create and manage assessments with practical tasks (not algorithmic puzzles).
- Provide an embedded AI agent in the workspace instead of separate chat or hint-level interactions.
- Track and evaluate AI token usage and efficiency during assessments.
- Automatically evaluate submitted code using predefined test cases.
- Record and summarize AI interactions and token consumption during assessments.
- Generate administrator-facing reports about performance, AI usage patterns, and token efficiency.
- Support User Acceptance Testing to validate the platform meets end-user needs.
- Support a workflow where implementation prompts and future code changes can be traced back to requirement IDs in this specification.

## 4. Non-Goals

- Real-time collaborative editing between multiple students.
- Full proctoring features such as webcam monitoring.
- Production-grade plagiarism detection.
- Support for all programming languages.
- A fully autonomous hiring decision system.
- Fine-grained code authorship attribution at token or line level.
- Leetcode-style algorithmic puzzle questions.
- Separate AI chat panel with hint levels (concept hint, strategy hint, etc.).
- AI Rescue button with misleading suggestions.
- Unrestricted free-form AI chat separate from the embedded agent.

---

## 5. Stakeholders

- **Students / Candidates**: complete practical coding assessments using the embedded AI agent.
- **Administrators / Instructors / Interviewers**: create assessments with practical tasks, review results, analyze AI usage and token efficiency.
- **Developers / Maintainers**: implement and maintain the platform.
- **Course Staff / Hiring Teams**: consume results for grading or screening.

---

## 6. User Stories

### US-01 Assessment Creation

As an administrator, I want to create and manage coding assessments with practical development tasks so that I can evaluate students in a realistic setting.

### US-02 Online Coding

As a student, I want to solve practical development tasks in a browser-based IDE so that I do not need to configure a local environment during the assessment.

### US-03 Safe Code Execution

As a platform operator, I want student code to run in an isolated sandbox so that the system remains secure and stable.

### US-04 Multi-Language Support

As a student, I want to solve tasks in supported languages such as Python or JavaScript/TypeScript so that the platform matches common assessment needs.

### US-05 Embedded AI Agent

As a student, I want an AI agent embedded in my workspace that understands my code context and can help me complete the task, so that I work in a realistic AI-assisted development setting.

### US-06 Automated Evaluation

As an administrator, I want submissions to be evaluated automatically using test cases so that scoring is fast, consistent, and reproducible. The evaluation relies on test code defined by the administrator.

### US-07 AI Usage and Token Efficiency Review

As an administrator, I want to review how AI was used during an assessment, including total token consumption and usage efficiency, so that I can judge whether students use AI strategically.

### US-08 Reporting

As an administrator, I want performance reports, AI usage summaries, and token efficiency metrics so that I can compare users and review outcomes efficiently.

### US-09 User Acceptance Testing

As a developer, I want structured UAT procedures so that we can validate the platform meets end-user requirements before release.

---

## 7. Functional Requirements

### 7.1 Authentication and Access Control

- **REQ-01** The system shall support authenticated access for at least two roles: student and administrator.
- **REQ-02** When a user attempts to access a protected page without authentication, the system shall require login before access is granted.
- **REQ-03** When a student is authenticated, the system shall only allow access to student-relevant features.
- **REQ-04** When an administrator is authenticated, the system shall allow access to assessment management, submission review, and reporting features.
- **REQ-05** If a user attempts to access a feature outside their role permissions, then the system shall deny access.

### 7.2 Assessment Management

- **REQ-06** The system shall allow an administrator to create an assessment with a title, description, duration, and status.
- **REQ-07** The system shall allow an administrator to edit an existing assessment.
- **REQ-08** The system shall allow an administrator to archive or delete an assessment.
- **REQ-09** When an administrator adds tasks to an assessment, the system shall associate those tasks with that assessment.
- **REQ-10** The system shall allow an administrator to set whether an assessment is draft, active, closed, or archived.
- **REQ-11** The system shall allow an administrator to enable or disable the embedded AI agent for an assessment.

### 7.3 Practical Task Management

- **REQ-12** The system shall allow an administrator to create a practical development task.
- **REQ-13** The system shall store for each task at least: title, task description, task type (web application, database, API development, bug fix, etc.), supported language(s), difficulty, and grading configuration.
- **REQ-14** Where a starter project is provided, the system shall present the starter project files in the coding workspace.
- **REQ-15** The system shall allow an administrator to define one or more test cases for each task.
- **REQ-16** The system shall allow an administrator to edit or remove a task before or after association with an assessment.
- **REQ-17** Task types shall include at least: web application development, database query/schema tasks, REST API development, and bug fixing in existing code.
- **REQ-18** Tasks shall not be limited to algorithmic puzzles. The system shall support tasks that require working with existing codebases, frameworks, and real-world patterns.

### 7.4 Student Assessment Participation

- **REQ-19** When a student opens an available assessment, the system shall display the assessment title, description, time information, and available tasks.
- **REQ-20** The system shall allow a student to open a task and view its description and starter project in the browser.
- **REQ-21** The system shall allow a student to write and edit code in the browser during an assessment attempt.
- **REQ-22** The system shall allow a student to run code before final submission.
- **REQ-23** When a student submits a solution, the system shall store the submission and trigger evaluation.
- **REQ-24** If an assessment is closed or expired, then the system shall prevent new submissions.

### 7.5 IDE-Like Workspace

- **REQ-25** The system shall provide a browser-based code editor.
- **REQ-26** The editor shall support syntax highlighting for all supported languages.
- **REQ-27** The workspace shall provide an output or console area for program output and runtime errors.
- **REQ-28** The workspace shall provide a visible action for code execution.
- **REQ-29** While an assessment attempt is active, the system shall preserve the student's in-progress code during normal page interactions.
- **REQ-30** The workspace shall support multi-file editing when a starter project contains multiple files.

### 7.6 Embedded AI Agent

- **REQ-31** The system shall provide an embedded AI agent within the coding workspace.
- **REQ-32** The embedded AI agent shall have access to the student's current code context (active file content, task description, selected language).
- **REQ-33** The AI agent shall be able to suggest code edits, explain concepts, assist with debugging, and answer questions related to the task.
- **REQ-34** The AI agent shall operate within the workspace UI, not as a separate external chat panel.
- **REQ-35** When a student interacts with the embedded AI agent, the system shall log the interaction with the associated user, assessment, task, token usage, prompt, response, and timestamp.
- **REQ-36** If the embedded AI agent is disabled for a given assessment, then the system shall hide the AI agent features for that assessment.
- **REQ-37** The embedded AI agent shall not provide direct complete solutions. It shall assist, explain, and guide rather than generate entire task solutions.

### 7.7 AI Token Usage and Efficiency Tracking

- **REQ-38** The system shall track the number of input tokens and output tokens consumed by each AI interaction.
- **REQ-39** The system shall calculate and store the total token usage per student per assessment attempt.
- **REQ-40** The system shall calculate and store the total token usage per student per task within an assessment.
- **REQ-41** The administrator report shall include token usage metrics: total tokens consumed, number of AI interactions, and average tokens per interaction.
- **REQ-42** The administrator report shall include a token efficiency indicator that helps administrators assess whether students used AI strategically or wastefully.
- **REQ-43** The system shall record which types of AI assistance were requested (code suggestion, explanation, debugging, etc.) alongside token costs.

### 7.8 Sandboxed Code Execution

- **REQ-44** The system shall execute user code in an isolated sandboxed environment.
- **REQ-45** If submitted code exceeds configured time or resource limits, then the system shall terminate execution and report the failure.
- **REQ-46** The system shall prevent user code from directly accessing sensitive server resources.
- **REQ-47** When code execution completes, the system shall return execution status, standard output, and runtime error information to the user when appropriate.

### 7.9 Language Support

- **REQ-48** The system shall support Python for code editing and execution.
- **REQ-49** The system shall support JavaScript and/or TypeScript for code editing and execution.
- **REQ-50** Where a task restricts allowed languages, the system shall enforce those restrictions during submission.
- **REQ-51** The first student-facing implementation shall support Python and JavaScript as student submission languages.

### 7.10 Automated Evaluation

- **REQ-52** When a solution is submitted, the system shall evaluate it against predefined test cases.
- **REQ-53** The system shall determine whether each relevant test case passes or fails.
- **REQ-54** The system shall calculate and store an overall result, score, or pass/fail outcome for the submission.
- **REQ-55** Where execution fails before all test cases run, the system shall store the failure status.

### 7.11 Submission and Result Storage

- **REQ-56** The system shall store each submission with its source code, selected language, timestamp, and related identifiers.
- **REQ-57** The system shall store evaluation results linked to the corresponding submission.
- **REQ-58** Where multiple submissions are allowed, the system shall preserve submission history.

### 7.12 Reporting and Analytics

- **REQ-59** The system shall provide an administrator-facing report view.
- **REQ-60** When an administrator selects an assessment, the system shall show submission outcomes for participating students.
- **REQ-61** The report shall include at least: student identifier, assessment identifier, submission status, score or result, AI usage summary, total token consumption, token efficiency indicator, and number of AI interactions.
- **REQ-62** The system should provide simple aggregate statistics for each assessment, such as average score, completion counts, and average token usage.

### 7.13 AI Usage Tracking

- **REQ-63** The system shall record AI usage events during an assessment.
- **REQ-64** The system shall store at least the timestamp, user, assessment context, task context, interaction type, token counts (input and output), and AI response for each AI usage event.
- **REQ-65** The system shall make AI usage summaries and token metrics visible to administrators.

### 7.14 Assessment Attempt and Identity Scope

- **REQ-66** The backend shall identify the current user from the authentication context, such as JWT or another secure token, rather than relying on a frontend-supplied user identifier.
- **REQ-67** When a student starts or resumes an assessment, the backend shall resolve the active assessment attempt from the authenticated user and assessment identifier.
- **REQ-68** The frontend shall not be responsible for creating, storing, trusting, or sending a real `session_id`; backend-connected assessment flows shall use assessment-scoped APIs where the backend resolves the active attempt from authentication context.
- **REQ-69** Workspace code shall be scoped to the authenticated user, assessment, and task so that each student sees only their own in-progress code.

Terminology clarification: older architecture material may use `session_id` for the assessment attempt identifier. In the current requirements, that concept is backend-owned and should be treated as an assessment attempt resolved from authenticated user and assessment ID. The frontend must not create, persist, send, or trust this identifier as authoritative state.

### 7.15 User Acceptance Testing

- **REQ-UAT-01** The project shall include a UAT plan document that defines test scenarios covering all major user workflows.
- **REQ-UAT-02** UAT scenarios shall cover at least: student assessment workflow (open, code, run, submit), administrator assessment creation workflow, administrator report review workflow, and embedded AI agent interaction workflow.
- **REQ-UAT-03** UAT shall be performed by team members acting as end users, not by the developers who implemented the feature.
- **REQ-UAT-04** UAT results shall be documented with pass/fail status, issues found, and screenshots where relevant.
- **REQ-UAT-05** Critical issues found during UAT shall be resolved before final submission.

---

## 8. Non-Functional Requirements

### 8.1 Security

- **NFR-01** The platform shall enforce authentication and role-based authorization.
- **NFR-02** The platform shall execute student code only in sandboxed environments.
- **NFR-03** The platform shall protect stored user, submission, and AI interaction data against unauthorized access.
- **NFR-04** The platform shall apply secure handling for AI-related data and secrets used for external AI APIs.

### 8.2 Usability

- **NFR-05** The student workflow shall be simple enough to complete an assessment without local environment setup.
- **NFR-06** The administrator workflow shall clearly separate authoring, monitoring, and reporting activities.
- **NFR-07** The interface should remain understandable for first-time users.
- **NFR-08** The embedded AI agent UI should be intuitive and not obstruct the code editing experience.

### 8.3 Reliability

- **NFR-09** The system should save submissions and evaluation results reliably.
- **NFR-10** If execution fails unexpectedly, then the platform should fail gracefully and preserve already saved assessment data.
- **NFR-11** If the AI provider service fails, then the platform should preserve the assessment attempt and show an actionable error message instead of losing student work.

### 8.4 Performance

- **NFR-12** The coding workspace should remain responsive during normal assessment usage.
- **NFR-13** Execution feedback should be returned within a reasonable time for normal-sized assessment tasks.
- **NFR-14** Report pages should load within a reasonable time for normal course or interview cohorts.
- **NFR-15** AI agent responses should return within a reasonable time or fail gracefully.

### 8.5 Maintainability and Extensibility

- **NFR-16** The system should be modular enough to separate authentication, assessment management, execution, AI integration, and reporting concerns.
- **NFR-17** The project should be structured so that new languages or AI features can be added later with limited impact on existing modules.

### 8.6 Auditability and Traceability

- **NFR-18** Requirements, implementation prompts, code changes, tests, and review notes should be traceable through requirement IDs where practical.
- **NFR-19** Changes to requirements should be versioned alongside implementation artifacts.

---

## 9. Constraints

- The system must support at least student and administrator roles.
- The system must support Python and JavaScript as student coding languages in the first implementation.
- The full system must provide sandboxed code execution.
- The system must provide performance reporting for administrators.
- Tasks must be practical development tasks, not algorithmic puzzles.
- AI assistance must be provided through an embedded agent in the workspace, not through separate chat or hint-level panels.
- AI token usage must be tracked and reported.
- The project must include User Acceptance Testing.
- AI assistance requires access to an external LLM API or integrated model service.
- The project should remain implementable within a university group-project scope.

---

## 10. Assumptions

- Users have access to a modern web browser and stable internet connection.
- The platform can access a secure runtime or container-based execution environment.
- AI assistance is available through an external API (e.g., Deepseek) or integrated model service.
- The first version targets a working prototype rather than a production-scale enterprise platform.
- Administrators review and create practical tasks that simulate real development work.

---

## 11. Acceptance Summary by Capability

### A. Assessment Authoring

Accepted when an administrator can create an assessment, add practical development tasks with starter projects, configure test cases, and activate the assessment.

### B. Student Coding Workflow

Accepted when a student can open an assessment, read a practical task description, view starter project files, write code, use the embedded AI agent, run code, and submit a solution.

### C. Safe Execution

Accepted when code runs in an isolated environment and failing executions are reported without compromising the platform.

### D. Embedded AI Agent

Accepted when students can interact with an AI agent embedded in the workspace that understands their code context, and all AI interactions and token usage are logged.

### E. Token Efficiency Tracking

Accepted when the system tracks AI token consumption per student per task, and administrators can view token usage metrics and efficiency indicators in reports.

### F. Reporting

Accepted when administrators can review per-student results, AI usage summaries, and token efficiency metrics for a selected assessment.

### G. User Acceptance Testing

Accepted when UAT is planned, executed by non-implementing team members, documented with results, and critical issues are resolved.

---

## 12. Traceability Notes

- Implementation prompts, code changes, tests, and review notes should reference one or more requirement IDs where practical.
- Verification artifacts or test cases should reference the requirement or requirements they validate.
- UI implementation should use `ui-style-reference.md` where relevant, but that file is a visual style reference only and must not override this specification.

---

## 13. Open Questions and Resolved Decisions

- Resolved: tasks are practical development tasks, not algorithmic puzzles (tutor feedback 2026-06-03).
- Resolved: AI assistance is provided through an embedded agent in the workspace, not through chat or hint levels (tutor feedback 2026-06-03).
- Resolved: AI Rescue button feature is not implemented (tutor feedback 2026-06-03).
- Resolved: AI token usage and efficiency must be tracked and reported (tutor feedback 2026-06-03).
- Resolved: User Acceptance Testing is required (tutor feedback 2026-06-03).
- Resolved: authentication identifies the user, and the backend resolves the active assessment attempt from authenticated user and assessment ID.
- Resolved: the frontend does not manage a real `session_id`.

---

## 14. Milestones

- **M1** Authentication and role-based access.
- **M2** Assessment and practical task authoring.
- **M3** Browser-based IDE workspace with multi-file support.
- **M4** Sandboxed execution for supported student languages.
- **M5** Embedded AI agent integration and token tracking.
- **M6** Automated evaluation and reporting with token efficiency metrics.
- **M7** User Acceptance Testing.

---

## 15. Summary

This requirements document defines a structured, testable basis for building an AI-assisted coding assessment platform. It emphasizes practical development tasks over algorithmic puzzles, an embedded AI agent over separate chat interfaces, and AI token efficiency tracking as a core evaluation metric. The specification incorporates tutor feedback to ensure the platform evaluates how effectively students use AI tools in realistic development scenarios.
