export function toLocalDateTimeInput(isoValue?: string | null) {
  const date = isoValue ? new Date(isoValue) : new Date(Date.now() + 60 * 60 * 1000);
  const localTime = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localTime.toISOString().slice(0, 16);
}

export function toUtcIso(localValue: string) {
  return localValue ? new Date(localValue).toISOString() : null;
}

export function hasAssessmentStarted(startsAt?: string | null) {
  return !startsAt || new Date(startsAt).getTime() <= Date.now();
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
