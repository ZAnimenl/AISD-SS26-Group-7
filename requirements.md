# requirements.md

## 1. Overview

### 1.1 Project
Online AI-Coding Assessment / Interview Platform

### 1.2 Problem Statement
Traditional coding assessments mainly evaluate the final code output. In an AI-assisted development setting, this is no longer sufficient. The platform must support realistic AI-assisted coding workflows while still allowing administrators to assess candidate performance, code quality, and AI usage behavior.

### 1.3 Objective
Build a web-based platform that lets administrators create and manage coding assessments, lets students complete them in a browser-based IDE-like environment with AI support, executes code safely in a sandbox, and produces reports that summarize both assessment outcomes and AI interaction patterns.

### 1.4 Scope
This document defines the **requirements** for the system in a structured, testable form suitable for spec-driven development. It focuses on stakeholder needs, user stories, acceptance criteria, constraints, and verifiable requirement statements.

---

## 2. Goals
- Provide a realistic AI-assisted coding assessment environment.
- Support browser-based coding for at least Python and JavaScript/TypeScript.
- Allow administrators to create and manage assessments and questions.
- Automatically evaluate submitted code using predefined test cases.
- Record and summarize AI interactions during assessments.
- Generate administrator-facing reports about performance and AI usage.
- Support a workflow that is compatible with later **design.md** and **tasks.md** artifacts.

## 3. Non-Goals
- Real-time collaborative editing between multiple students.
- Full proctoring features such as webcam monitoring.
- Production-grade plagiarism detection.
- Support for all programming languages.
- A fully autonomous hiring decision system.
- Fine-grained code authorship attribution beyond basic AI usage logging.

---

## 4. Stakeholders
- **Students / Candidates**: complete coding assessments.
- **Administrators / Instructors / Interviewers**: create assessments, review results, analyze AI usage.
- **Developers / Maintainers**: implement and maintain the platform.
- **Course Staff / Hiring Teams**: consume results for grading or screening.

---

## 5. User Stories

### US-01 Assessment Creation
As an administrator, I want to create and manage coding assessments so that I can evaluate students or candidates in a structured way.

### US-02 Online Coding
As a student, I want to solve coding tasks in a browser-based IDE so that I do not need to configure a local environment during the assessment.

### US-03 Safe Code Execution
As a platform operator, I want student code to run in an isolated sandbox so that the system remains secure and stable.

### US-04 Multi-Language Support
As a student, I want to solve tasks in supported languages such as Python or JavaScript/TypeScript so that the platform matches common assessment needs.

### US-05 AI-Assisted Problem Solving
As a student, I want access to AI assistance during the assessment so that I can work in a realistic modern software development setting.

### US-06 Automated Evaluation
As an administrator, I want submissions to be evaluated automatically using test cases so that scoring is fast, consistent, and reproducible.
The Evaluation relies on test code defined by the administrator.

### US-07 AI Usage Review
As an administrator, I want to review how AI was used during an assessment so that I can judge process as well as final output.

### US-08 Reporting
As an administrator, I want performance reports and AI usage summaries so that I can compare users and review outcomes efficiently.

---

## 6. Functional Requirements (EARS)

### 6.1 Authentication and Access Control
- **REQ-01** The system shall support authenticated access for at least two roles: **student** and **administrator**.
- **REQ-02** When a user attempts to access a protected page without authentication, the system shall require login before access is granted.
- **REQ-03** When a student is authenticated, the system shall only allow access to student-relevant features.
- **REQ-04** When an administrator is authenticated, the system shall allow access to assessment management, submission review, and reporting features.
- **REQ-05** If a user attempts to access a feature outside their role permissions, then the system shall deny access.

### 6.2 Assessment Management
- **REQ-06** The system shall allow an administrator to create an assessment with a title, description, duration, and status.
- **REQ-07** The system shall allow an administrator to edit an existing assessment.
- **REQ-08** The system shall allow an administrator to archive or delete an assessment.
- **REQ-09** When an administrator adds questions to an assessment, the system shall associate those questions with that assessment.
- **REQ-10** The system shall allow an administrator to set whether an assessment is draft, active, or closed.

### 6.3 Question Management
- **REQ-11** The system shall allow an administrator to create a coding question.
- **REQ-12** The system shall store for each question at least: title, problem description, supported language(s), and grading configuration.
- **REQ-13** Where starter code is provided, the system shall present that starter code in the coding workspace.
- **REQ-14** The system shall allow an administrator to define one or more test cases for each question.
- **REQ-15** The system shall allow an administrator to edit or remove a question before or after association with an assessment.

### 6.4 Student Assessment Participation
- **REQ-16** When a student opens an available assessment, the system shall display the assessment title, description, time information, and available questions.
- **REQ-17** The system shall allow a student to open a question and view its problem statement in the browser.
- **REQ-18** The system shall allow a student to write and edit code in the browser during an assessment attempt.
- **REQ-19** The system shall allow a student to run code before final submission.
- **REQ-20** When a student submits a solution, the system shall store the submission and trigger evaluation.
- **REQ-21** If an assessment is closed or expired, then the system shall prevent new submissions.

### 6.5 IDE-Like Workspace
- **REQ-22** The system shall provide a browser-based code editor.
- **REQ-23** The editor shall support syntax highlighting for all supported languages.
- **REQ-24** The workspace shall provide an output or console area for program output and runtime errors.
- **REQ-25** The workspace shall provide a visible action for code execution.
- **REQ-26** While an assessment attempt is active, the system shall preserve the student's in-progress code during normal page interactions.

### 6.6 Sandboxed Code Execution
- **REQ-27** The system shall execute user code in an isolated sandboxed environment.
- **REQ-28** If submitted code exceeds configured time or resource limits, then the system shall terminate execution and report the failure.
- **REQ-29** The system shall prevent user code from directly accessing sensitive server resources.
- **REQ-30** When code execution completes, the system shall return execution status, standard output, and runtime error information to the user when appropriate.

MVP clarification: the first MVP does not satisfy real sandboxed execution by running submitted code. It uses mocked execution only and must not use `eval`, `child_process`, Docker, or local language runtimes for student submissions. Real sandboxed execution is future work.

### 6.7 Language Support
- **REQ-31** The system shall support **Python** for code editing and execution.
- **REQ-32** The system shall support **JavaScript and/or TypeScript** for code editing and execution.
- **REQ-33** Where a question restricts allowed languages, the system shall enforce those restrictions during submission.

MVP clarification: first MVP student languages are Python and JavaScript only. TypeScript is used for the project codebase, but TypeScript submissions are not executable in the first MVP.

### 6.8 AI Assistance
- **REQ-34** The system shall provide AI assistance during assessments.
- **REQ-35** Where AI assistance is enabled, the system shall support at least one of the following interaction types: explanation, debugging help, code hinting, or chat-based assistance.
- **REQ-36** When a student uses AI assistance, the system shall log the interaction with the associated user, assessment, and timestamp.
- **REQ-37** If AI assistance is disabled for a given assessment, then the system shall prevent access to AI assistance features for that assessment.

### 6.9 Automated Evaluation
- **REQ-38** When a solution is submitted, the system shall evaluate it against predefined test cases.
- **REQ-39** The system shall determine whether each relevant test case passes or fails.
- **REQ-40** The system shall calculate and store an overall result, score, or pass/fail outcome for the submission.
- **REQ-41** Where execution fails before all test cases run, the system shall store the failure status.

### 6.10 Submission and Result Storage
- **REQ-42** The system shall store each submission with its source code, selected language, timestamp, and related identifiers.
- **REQ-43** The system shall store evaluation results linked to the corresponding submission.
- **REQ-44** Where multiple submissions are allowed, the system shall preserve submission history.

### 6.11 Reporting and Analytics
- **REQ-45** The system shall provide an administrator-facing report view.
- **REQ-46** When an administrator selects an assessment, the system shall show submission outcomes for participating students.
- **REQ-47** The report shall include at least student identifier, assessment identifier, submission status, score or result, and AI usage summary.
- **REQ-48** The system should provide simple aggregate statistics for each assessment, such as average score or completion counts.

### 6.12 AI Usage Tracking
- **REQ-49** The system shall record AI usage events during an assessment.
- **REQ-50** The system shall store at least the timestamp and assessment context of each AI usage event.
- **REQ-51** Where technically feasible, the system should record the type of AI assistance requested.
- **REQ-52** The system shall make AI usage summaries visible to administrators.


### 6.13 Assessment Attempt and Identity Scope
- **REQ-53** The backend shall identify the current user from the authentication context, such as JWT or another secure token, rather than relying on a frontend-supplied user identifier.
- **REQ-54** When a student starts or resumes an assessment, the backend shall resolve the active assessment attempt from the authenticated user and assessment identifier.
- **REQ-55** The frontend shall not be responsible for creating, storing, or trusting a real `session_id`; any frontend-held attempt/session value shall be either mock-only state or a backend-returned transient compatibility value kept in memory only.
- **REQ-56** Workspace code shall be scoped to the authenticated user, assessment, and question so that each student sees only their own in-progress code.

Terminology clarification: older architecture material may use `session_id` for the assessment attempt identifier. In the current requirements, that concept is backend-owned and should be treated as an assessment attempt/session resolved from authenticated user + assessment_id. The frontend must not create, persist, or trust this identifier as authoritative state. If an interim backend endpoint still requires a session-shaped value, the frontend may hold the backend-returned attempt identifier in memory only for the duration of the page flow.
---

## 7. Non-Functional Requirements

### 7.1 Security
- **NFR-01** The platform shall enforce authentication and role-based authorization.
- **NFR-02** The platform shall execute student code only in sandboxed environments.
- **NFR-03** The platform shall protect stored user, submission, and AI interaction data against unauthorized access.
- **NFR-04** The platform shall apply secure handling for AI-related data and secrets used for external AI APIs.

MVP clarification for NFR-02: first MVP avoids unsafe execution by not executing student code at all. Mocked execution is the temporary safety boundary until real sandboxing is implemented later.

### 7.2 Usability
- **NFR-05** The student workflow shall be simple enough to complete an assessment without local environment setup.
- **NFR-06** The administrator workflow shall clearly separate authoring, monitoring, and reporting activities.
- **NFR-07** The interface should remain understandable for first-time users.

### 7.3 Reliability
- **NFR-08** The system should save submissions and evaluation results reliably.
- **NFR-09** If execution fails unexpectedly, then the platform should fail gracefully and preserve already saved assessment data.

### 7.4 Performance
- **NFR-10** The coding workspace should remain responsive during normal assessment usage.
- **NFR-11** Execution feedback should be returned within a reasonable time for normal-sized assessment tasks.
- **NFR-12** Report pages should load within a reasonable time for normal course or interview cohorts.

### 7.5 Maintainability and Extensibility
- **NFR-13** The system should be modular enough to separate authentication, assessment management, execution, AI integration, and reporting concerns.
- **NFR-14** The project should be structured so that new languages or AI features can be added later with limited impact on existing modules.

### 7.6 Auditability and Traceability
- **NFR-15** Requirements, implementation, and tests should be traceable through requirement IDs.
- **NFR-16** Changes to requirements should be versioned alongside implementation artifacts.

---

## 8. Constraints
- The system must support at least **student** and **administrator** roles.
- The system must support **Python** and **JavaScript** as student coding languages in the first MVP.
- The full system must provide **sandboxed code execution**. The first MVP must use mocked execution only.
- The system must provide **performance reporting for administrators**.
- The project should remain implementable within a university group-project scope.

---

## 9. Assumptions
- Users have access to a modern web browser and stable internet connection.
- The platform can access a secure runtime or container-based execution environment.
- AI assistance is available through an external API or integrated model service.
- The first version is an MVP rather than a production-scale enterprise platform.

---

## 10. Acceptance Summary by Capability

### A. Assessment Authoring
Accepted when an administrator can create an assessment, add questions, configure test cases, and activate the assessment.

### B. Student Coding Workflow
Accepted when a student can open an assessment, read a question, write code, run code, and submit a solution.

### C. Safe Execution
Accepted when code runs in an isolated environment and failing executions are reported without compromising the platform.

### D. AI Support and Logging
Accepted when AI assistance can be used in enabled assessments and the related interaction events are stored and reviewable.

### E. Reporting
Accepted when administrators can review per-student results and AI usage summaries for a selected assessment.

---

## 11. Traceability Notes
- Each implementation task in **tasks.md** should reference one or more `REQ-*` or `NFR-*` identifiers.
- Each verification artifact or test case should reference the requirement(s) it validates.
- Future **design.md** content should explain how components satisfy the requirement groups in Sections 6 and 7.

---

## 12. Open Questions
- Resolved for MVP: AI usage does not affect grading and only appears in reports.
- Resolved for MVP: students see simple immediate feedback after submission: submitted, passed/failed/error, score, stdout, and stderr.
- Resolved for MVP: TypeScript is not a student submission language. Student coding languages are Python and JavaScript.
- Resolved for MVP: administrators can enable or disable AI assistance per assessment using `Assessment.aiEnabled`.
- Resolved for backend alignment: authentication identifies the user; the backend resolves the active assessment attempt from authenticated user + assessment_id. The frontend MVP does not manage a real `session_id`.

---

## 13. Milestones (High-Level)
- **M1** Authentication and role-based access
- **M2** Assessment and question authoring
- **M3** Browser-based IDE workspace
- **M4** Mocked execution for Python and JavaScript in the first MVP; real sandboxed execution later
- **M5** AI assistance and logging
- **M6** Automated evaluation and reporting

---

## 14. Summary
This requirements document defines a structured, testable basis for building an AI-assisted coding assessment platform. It emphasizes stakeholder intent, explicit scope boundaries, EARS-style requirement statements, traceability, and later refinement into design and task artifacts.



