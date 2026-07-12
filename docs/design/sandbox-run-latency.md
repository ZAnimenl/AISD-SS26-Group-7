# Sandbox Run Latency

## Problem Definition

Warm public runs for normal-sized assessment tasks currently take tens of
seconds. Browser-preview tasks amplify the delay because the preview harness
and each public check start separate Jest processes inside one resource-limited
shared container. A cold request can also wait without a bound while the grader
image is built.

The target is warm, non-timeout Run feedback within ten seconds while retaining
Docker isolation, network blocking, resource limits, cleanup, public/hidden
test separation, and the existing execution-result API.

## Options

### Keep one shared container and increase its resources

Rejected. This improves contention but retains cross-run workspace visibility
and lets unrelated students compete inside one isolation boundary.

### Combine every public check into one language-specific test process

Deferred. It can reduce runtime startup further, but it requires reliable
per-test result and output mapping for Python, JavaScript, TypeScript, HTML, and
SQL.

### Ephemeral per-check containers with local staging

Selected. Each check receives a short-lived container that mounts only its own
generated workspace. Files are copied to container-local temporary storage
before the test command runs, the preview artifact is copied back, and the
container is force-removed. Checks within one evaluation may run concurrently
under a global container limit.

## State Machine

- `image_unknown` -> `image_warming`: startup or the first readiness request
  begins one shared image-readiness task.
- `image_warming` -> `image_ready`: the pinned grader image exists.
- `image_warming` -> `image_failed`: image inspection/build fails; a later
  readiness request may retry.
- `queued` -> `container_starting`: image is ready and an execution slot is
  available.
- `container_starting` -> `staging`: an isolated container starts with only the
  current check directory mounted.
- `staging` -> `running`: files have been copied to container-local storage.
- `running` -> `completed`: output and any preview artifact are collected.
- `running` -> `timed_out`: the bounded host or in-container deadline expires.
- Any per-check state -> `cleanup`: the ephemeral container and temporary files
  are removed.
- `cleanup` -> terminal success or failure result.

If image warmup is still running, a student request waits briefly and then
returns an actionable unavailable result instead of waiting for the full image
build. The background warmup continues.

## Impact Surface

- Module 3: grader image readiness, per-check container lifecycle, workspace
  staging, resource limits, timeout, and cleanup.
- Module 1 integration: the existing run/submit evaluation calls and response
  schema remain unchanged.
- Module 2: workspace data renders without waiting for a Docker probe, and
  duplicate start requests during immediate SPA navigation are suppressed.
- Data, roles, permissions, scores, and hidden-test serialization are unchanged.

## Security Boundaries

- Student code still runs only inside Docker.
- Containers have no network, drop Linux capabilities, and retain CPU/memory
  limits.
- A container mounts only one generated check directory, so it cannot traverse
  sibling workspaces from concurrent public or hidden checks.
- Hidden test inputs, expected outputs, and grading code remain absent from
  student-facing responses.

## Rollback

Revert the ephemeral container lifecycle and workspace staging changes to the
previous persistent-container execution path. The API and persisted execution
schema require no rollback or migration.

## Primitive Acceptance Criteria

- Once the grader image is warm, the canonical browser preview plus its public
  checks returns in under ten seconds on supported local development hardware.
- A cold image build never holds a student Run request for minutes; the request
  receives a quick retryable unavailable result while warmup continues.
- Every configured public or hidden check still produces its own pass/fail
  result and contributes to scoring exactly once.
- Timed-out checks are terminated and their containers are removed.
- Concurrent checks cannot read sibling run directories.
- Preview HTML is returned only from the sandbox-produced preview artifact.
