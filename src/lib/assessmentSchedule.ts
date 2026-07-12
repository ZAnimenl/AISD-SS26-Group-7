export function toLocalDateTimeInput(isoValue?: string | null) {
  const date = isoValue ? new Date(isoValue) : new Date(Date.now() + 60 * 60 * 1000);
  const localTime = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localTime.toISOString().slice(0, 16);
}

export function toUtcIso(localValue: string) {
  return localValue ? new Date(localValue).toISOString() : null;
}

export function currentUtcIso() {
  return new Date().toISOString();
}

export function hasAssessmentStarted(startsAt?: string | null) {
  return !startsAt || new Date(startsAt).getTime() <= Date.now();
}

export function hasAssessmentExpired(expiresAt?: string | null) {
  return Boolean(expiresAt) && new Date(expiresAt!).getTime() <= Date.now();
}

type ScheduledAssessment = {
  status: string;
  attempt_status?: string;
  starts_at?: string | null;
  expires_at?: string | null;
};

export function isAssessmentAvailableNow(assessment: ScheduledAssessment) {
  return (
    assessment.status === "active" &&
    assessment.attempt_status !== "expired" &&
    hasAssessmentStarted(assessment.starts_at) &&
    !hasAssessmentExpired(assessment.expires_at)
  );
}

/** Derive the badge status shown in listings. An assessment marked "active" in the
 *  DB is really scheduled until starts_at and really expired past expires_at. */
export function effectiveAssessmentStatus(assessment: ScheduledAssessment) {
  if (assessment.status === "active") {
    if (hasAssessmentExpired(assessment.expires_at)) {
      return "expired";
    }
    if (!hasAssessmentStarted(assessment.starts_at)) {
      return "scheduled";
    }
  }
  return assessment.status;
}

export function partitionAssessments<T extends ScheduledAssessment>(assessments: T[]) {
  return assessments.reduce<{ available: T[]; other: T[] }>(
    (groups, assessment) => {
      groups[isAssessmentAvailableNow(assessment) ? "available" : "other"].push(assessment);
      return groups;
    },
    { available: [], other: [] }
  );
}

export function formatAssessmentExpiry(expiresAt?: string | null) {
  if (!expiresAt) {
    return "No deadline";
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(expiresAt));
}

export function defaultAssessmentExpiry() {
  return toLocalDateTimeInput(new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString());
}

export function formatAssessmentStart(startsAt?: string | null) {
  if (!startsAt) {
    return "Available now";
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(startsAt));
}
