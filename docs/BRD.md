# Business Requirements Document

## Purpose

The platform supports realistic AI-assisted coding assessments. Administrators
create practical tasks, students complete them in a browser workspace, the
system evaluates submissions safely, and reports summarize results and AI usage.

## Business Goals

- Evaluate practical development skills instead of algorithm-only puzzle
  performance.
- Measure how students use embedded AI assistance during assessment work.
- Let administrators review task quality, submissions, AI interaction evidence,
  and automatically graded AI-assisted working behavior.
- Keep functional correctness and AI usage as separate, understandable scores.
- Keep student work inside the assessment website without local setup.

## Stakeholders

- Students and candidates complete assessments.
- Administrators, instructors, and interviewers create assessments and review
  results.
- Platform maintainers operate the web app, backend, database, sandbox, and AI
  integration.

## In Scope

- Role-based student and administrator workflows.
- Practical task authoring and assessment management.
- Browser-based coding workspace.
- Sandboxed execution and grading.
- Embedded AI assistance with telemetry.
- Automatic AI usage grading and timed submission reflections for AI-enabled
  assessments.
- Reporting for functional performance, AI usage, and the combined result.

## Out of Scope

- Real-time collaborative editing.
- Full proctoring.
- Production plagiarism detection.
- Autonomous hiring decisions.
- Student-local project installation requirements.

## Success Measures

- Administrators can create and publish practical assessments.
- Students can complete tasks without local dependency setup.
- Submissions are evaluated without exposing hidden tests.
- AI-disabled assessments produce a Functional Score only.
- AI-enabled assessments produce separate Functional and AI Usage scores plus
  their arithmetic mean as the Final Score.
- AI interactions, reflections, grading evidence, and token totals are visible
  in administrator reports.
