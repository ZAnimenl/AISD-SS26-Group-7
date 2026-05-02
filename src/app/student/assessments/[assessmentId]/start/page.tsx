"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Clock, Sparkles } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAssessment, startAssessment } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function AssessmentStartPage({ params }: { params: { assessmentId: string } }) {
  const router = useRouter();
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAssessment(params.assessmentId).then(setAssessment).catch(() => router.replace("/login"));
  }, [params.assessmentId, router]);

  async function openWorkspace() {
    setError(null);
    try {
      await startAssessment(params.assessmentId);
      router.push(`/student/assessments/${params.assessmentId}/workspace`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to start assessment.");
    }
  }

  if (!assessment) {
    return <SectionHeader eyebrow="Start assessment" title="Connecting to backend..." />;
  }

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
            <button className="btn-primary mt-8" onClick={openWorkspace}>Start attempt</button>
            {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
          </div>
        </section>
        <aside className="panel">
          <h2 className="relative text-lg font-semibold">Questions</h2>
          <div className="relative mt-4 space-y-3">
            {(assessment.questions.length ? assessment.questions : [{ question_id: "placeholder", title: "Questions load in workspace", constraints: [], language_constraints: ["python", "javascript"] }]).map((question, index) => (
              <div key={question.question_id} className="rounded-xl border border-white/10 bg-black/20 p-4">
                <p className="text-xs text-cyanGlow/70">Question {index + 1}</p>
                <p className="mt-1 font-semibold">{question.title}</p>
                <p className="mt-2 text-xs text-white/45">Languages: Python, JavaScript</p>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}
