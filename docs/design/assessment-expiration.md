# Assessment expiration

## Decision

Assessments have an administrator-defined `ExpiresAt` timestamp in addition to `StartsAt` and `DurationMinutes`.

- `StartsAt` controls when students may begin.
- `ExpiresAt` is the hard assessment deadline.
- `DurationMinutes` controls the normal length of an individual attempt.
- A new attempt expires at the earlier of `StartedAt + DurationMinutes` or the assessment deadline.
- After the assessment deadline, students may view submitted results but may not start, continue, run, save, use AI, or submit assessment code.

## Boundaries

The Next.js UI only collects and displays schedule data. The .NET backend remains authoritative for schedule validation and enforcement. Sandbox execution remains isolated and receives work only for an open assessment attempt.

## Compatibility

Existing database rows may have a null assessment expiry after schema upgrade. New and edited assessments require an expiry. A nullable database column avoids inventing deadlines for historical data.
