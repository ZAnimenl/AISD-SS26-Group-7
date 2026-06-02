# Checker Overview

The checker is the backend grading flow that evaluates a student's submission against the question's test cases.

## Current Status

The checker is currently implemented as a backend-controlled Docker grading pipeline. It is not a frontend mock and it is not called directly by the frontend. The ASP.NET API owns authentication, attempt resolution, persistence, and response shaping; the Docker grader owns isolated code execution.

## Purpose

The checker answers one core question: does the submitted solution produce the expected result for the available test cases? It runs the code in the grading pipeline, collects execution output, and turns the result into a submission status and code-correctness score. Process-aware scoring is a separate reporting concern that can combine checker output with AI usage, reflection, and AI Rescue behavior.

## Where It Lives

- `Backend/Backend/Services/CodeEvaluationService.cs` orchestrates evaluation and score calculation.
- `Backend/Backend/Services/Grading/DockerCodeRunner.cs` executes code in the isolated grading environment.
- `Backend/Backend/Services/Grading/GraderCommandFactory.cs` builds the language-specific command used for checking.
- `Backend/Backend/Api/ExecutionEndpoints.cs` and `Backend/Backend/Api/SubmissionEndpoints.cs` expose the run and submit entry points.

## How It Works

1. The frontend sends the current code, language, assessment, and question context to the backend.
2. The backend loads the relevant test cases for the question.
3. The grading runner executes the solution in an isolated environment.
4. The checker compares execution results with the expected test behavior.
5. The backend returns a run result or stores a final submission result.

## Supported Languages

The checker currently supports:

- Python
- JavaScript
- TypeScript

## Inputs

The checker uses:

- the submitted source code
- the selected language
- the question's public or hidden test cases
- the assessment and question identifiers

## Outputs

The checker can produce:

- execution status
- standard output
- standard error
- per-test pass or fail results
- code-correctness score or pass/fail submission status

## Security Notes

- Student code is not executed in the frontend.
- The checker runs through the backend grading pipeline.
- Hidden test cases must stay private and must not be exposed to the student UI.
- The untrusted student code should remain isolated from the normal API process. The API may orchestrate grading, but execution happens in the Docker grader.

## Operational Notes

- The grading command currently uses a short timeout to avoid hanging checks.
- The local development backend is wired to Docker-based execution through the grading service.
- Docker must be available for real run/submit grading in local development. If Docker is unavailable, grading should fail safely and preserve existing persisted state.
- If a language is not supported, the checker should fail safely instead of guessing.

## Related Files

- `Backend/Backend/Services/CodeEvaluationService.cs`
- `Backend/Backend/Services/Grading/DockerCodeRunner.cs`
- `Backend/Backend/Services/Grading/GraderCommandFactory.cs`
- `Backend/Backend/Services/CodeEvaluationModels.cs`
- `Backend/Backend/Api/ExecutionEndpoints.cs`
- `Backend/Backend/Api/SubmissionEndpoints.cs`
