"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Clock, Loader2, PlayCircle, RotateCcw, Sparkles } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAssessment, getWorkspace, isAuthenticationError, startAssessment } from "@/lib/api";
import { getLanguageLabel, normalizeStudentLanguageConstraints } from "@/lib/languages";
import type { Assessment } from "@/lib/types";

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
    return <SectionHeader eyebrow="Start assessment" title="Connecting to backend..." />;
  }

  const canStartAttempt = assessment.status === "active";
  const startButtonLabel = assessment.attempt_status === "submitted" ? "Start another attempt" : "Start attempt";

  return (
    <div>
      <SectionHeader eyebrow="Start assessment" title={assessment.title} />
      <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <section className="panel">
          <div className="relative">
            <StatusBadge status={assessment.status} />
            <p className="mt-5 max-w-3xl text-lg leading-8 text-white/65">{assessment.description}</p>
            <div className="mt-6 grid gap-3 sm:grid-cols-3">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><Clock size={20} className="text-cyanGlow" /><p className="mt-3 text-2xl font-semibold">{assessment.duration_minutes} min</p><p className="text-sm text-white/45">Duration</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><Sparkles size={20} className="text-purpleGlow" /><p className="mt-3 text-2xl font-semibold">{assessment.questions.length || assessment.question_count}</p><p className="text-sm text-white/45">Questions</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><Sparkles size={20} className="text-cyanGlow" /><p className="mt-3 text-2xl font-semibold">{assessment.ai_enabled ? "On" : "Off"}</p><p className="text-sm text-white/45">AI assistance</p></div>
            </div>
            {canStartAttempt ? (
              <button className="btn-primary mt-8" onClick={openWorkspace} disabled={isStarting}>
                {isStarting ? <Loader2 className="animate-spin" size={16} /> : assessment.attempt_status === "submitted" ? <RotateCcw size={16} /> : <PlayCircle size={16} />}
                {isStarting ? "Opening workspace..." : startButtonLabel}
              </button>
            ) : (
              <p className="mt-8 text-sm text-white/50">This assessment is not open for new attempts.</p>
            )}
            {isStarting ? <p className="mt-3 text-sm text-white/55" aria-live="polite">Backend is resolving your real active attempt and workspace before opening the IDE.</p> : null}
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
