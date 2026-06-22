"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { CalendarClock, Clock, Loader2, PlayCircle, RotateCcw, Sparkles } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAssessment, getWorkspace, isAuthenticationError, startAssessment } from "@/lib/api";
import { getLanguageLabel, normalizeStudentLanguageConstraints } from "@/lib/languages";
import type { Assessment } from "@/lib/types";
import { formatAssessmentExpiry, formatAssessmentStart, hasAssessmentExpired, hasAssessmentStarted } from "@/lib/assessmentSchedule";

function formatQuestionLanguages(question: Assessment["questions"][number]) {
  return normalizeStudentLanguageConstraints(question.language_constraints, question.task_type)
    .map(getLanguageLabel)
    .join(", ");
}

export default function AssessmentStartPage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isStarting, setIsStarting] = useState(false);

  useEffect(() => {
    getAssessment(assessmentId).then(setAssessment).catch((exception) => {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to load assessment.");
    });
  }, [assessmentId, router]);

  async function openWorkspace() {
    if (isStarting) {
      return;
    }

    setError(null);
    setIsStarting(true);
    try {
      await startAssessment(assessmentId);
      await getWorkspace(assessmentId);
      router.push(`/student/assessments/${assessmentId}/workspace`);
    } catch (exception) {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to start assessment.");
      setIsStarting(false);
    }
  }

  if (error && !assessment) {
    return <SectionHeader eyebrow="Start assessment" title={error} />;
  }

  if (!assessment) {
    return <SectionHeader eyebrow="Start assessment" title="Loading assessment..." />;
  }

  const attemptExpired = assessment.attempt_status === "expired";
  const hasStarted = hasAssessmentStarted(assessment.starts_at);
  const assessmentExpired = hasAssessmentExpired(assessment.expires_at);
  const canStartAttempt = assessment.status === "active" && hasStarted && !assessmentExpired && !attemptExpired;
  const startButtonLabel = assessment.attempt_status === "active"
    ? "Continue attempt"
    : assessment.attempt_status === "submitted"
      ? "Start another attempt"
      : "Start attempt";

  return (
    <div>
      <SectionHeader eyebrow="Start assessment" title={assessment.title} />
      <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <section className="panel">
          <div className="relative">
            <StatusBadge status={assessment.status} />
            <p className="mt-5 max-w-3xl text-lg leading-8 text-white/65">{assessment.description}</p>
            <div className="mt-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><Clock size={20} className="text-cyanGlow" /><p className="mt-3 text-2xl font-semibold">{assessment.duration_minutes} min</p><p className="text-sm text-white/45">Duration</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><CalendarClock size={20} className="text-cyanGlow" /><p className="mt-3 text-base font-semibold">{formatAssessmentStart(assessment.starts_at)}</p><p className="text-sm text-white/45">Start time</p></div>
              <div className={`rounded-2xl border p-4 ${assessmentExpired ? "border-pinkGlow/25 bg-pinkGlow/[0.06]" : "border-cyanGlow/20 bg-cyanGlow/[0.05]"}`}>
                <CalendarClock size={20} className={assessmentExpired ? "text-pinkGlow" : "text-cyanGlow"} />
                <p className="mt-3 text-base font-semibold">{formatAssessmentExpiry(assessment.expires_at)}</p>
                <p className={`text-sm font-medium ${assessmentExpired ? "text-pinkGlow/80" : "text-cyanGlow/75"}`}>
                  {assessmentExpired ? "Expired" : "Available until"}
                </p>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><Sparkles size={20} className="text-purpleGlow" /><p className="mt-3 text-2xl font-semibold">{assessment.questions.length || assessment.question_count}</p><p className="text-sm text-white/45">Questions</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><Sparkles size={20} className="text-cyanGlow" /><p className="mt-3 text-2xl font-semibold">{assessment.ai_enabled ? "On" : "Off"}</p><p className="text-sm text-white/45">AI assistance</p></div>
            </div>
            <div className={`mt-5 rounded-2xl border px-4 py-3 ${assessmentExpired ? "border-pinkGlow/25 bg-pinkGlow/[0.06]" : "border-cyanGlow/20 bg-cyanGlow/[0.04]"}`}>
              <p className="text-sm font-medium text-white/80">
                Assessment availability: <span className={assessmentExpired ? "text-pinkGlow" : "text-cyanGlow"}>{formatAssessmentExpiry(assessment.expires_at)}</span>
              </p>
              <p className="mt-1 text-xs leading-5 text-white/45">
                {assessmentExpired
                  ? "This assessment is now review-only. New attempts and continued editing are unavailable."
                  : "You can work normally until this time. Afterward, the assessment becomes review-only."}
              </p>
            </div>
            {canStartAttempt ? (
              <button className="btn-primary mt-8" onClick={openWorkspace} disabled={isStarting}>
                {isStarting ? <Loader2 className="animate-spin" size={16} /> : assessment.attempt_status === "submitted" ? <RotateCcw size={16} /> : <PlayCircle size={16} />}
                {isStarting ? "Opening workspace..." : startButtonLabel}
              </button>
            ) : (
              <div className="mt-8">
                <p className="text-sm text-white/50">
                  {assessmentExpired
                    ? assessment.attempt_status === "submitted"
                      ? "This assessment has expired. You can review your submitted result, but cannot start another attempt."
                      : "This assessment has expired. New attempts and continued work are unavailable."
                    : attemptExpired
                    ? "This assessment attempt has expired and cannot be started again."
                    : !hasStarted
                    ? `This assessment opens ${formatAssessmentStart(assessment.starts_at)}.`
                    : "This assessment is not open for new attempts."}
                </p>
                {assessmentExpired && assessment.attempt_status === "submitted" ? (
                  <Link className="btn-secondary mt-4" href={`/student/assessments/${assessmentId}/review`}>
                    Review submitted result
                  </Link>
                ) : null}
              </div>
            )}
            {isStarting ? <p className="mt-3 text-sm text-white/55" aria-live="polite">Preparing your workspace...</p> : null}
            {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
          </div>
        </section>
        <aside className="panel">
          <h2 className="relative text-lg font-semibold">Questions</h2>
          <div className="relative mt-4 space-y-3">
            {assessment.questions.length ? (
              assessment.questions.map((question, index) => (
                <div key={question.question_id} className="rounded-xl border border-white/10 bg-black/20 p-4">
                  <p className="text-xs text-cyanGlow/70">Question {index + 1}</p>
                  <p className="mt-1 font-semibold">{question.title}</p>
                  <p className="mt-2 text-xs text-white/45">Languages: {formatQuestionLanguages(question)}</p>
                </div>
              ))
            ) : (
              <div className="rounded-xl border border-white/10 bg-black/20 p-4">
                <p className="text-sm text-white/55">No question details are available yet.</p>
              </div>
            )}
          </div>
        </aside>
      </div>
    </div>
  );
}
