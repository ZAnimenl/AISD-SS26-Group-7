import Link from "next/link";
import { Clock, Sparkles } from "lucide-react";
import { StatusBadge } from "@/components/ui/StatusBadge";
import type { Assessment } from "@/lib/types";

export function AssessmentCard({ assessment }: { assessment: Assessment }) {
  const actionHref =
    assessment.attempt_status === "submitted"
      ? `/student/assessments/${assessment.assessment_id}/review`
      : assessment.attempt_status === "active"
      ? `/student/assessments/${assessment.assessment_id}/workspace`
      : `/student/assessments/${assessment.assessment_id}/start`;

  return (
    <article className="panel">
      <div className="relative flex h-full flex-col">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-xl font-semibold text-white">{assessment.title}</h2>
            <p className="mt-2 text-sm leading-6 text-white/55">{assessment.description}</p>
          </div>
          <StatusBadge status={assessment.attempt_status ?? assessment.status} />
        </div>
        <div className="mt-5 flex flex-wrap gap-2 text-xs text-white/50">
          <span className="badge"><Clock size={13} /> {assessment.duration_minutes} min</span>
          <span className="badge">{assessment.question_count} questions</span>
          <span className="badge">{assessment.ai_enabled ? <Sparkles size={13} /> : null}{assessment.ai_enabled ? "AI enabled" : "AI disabled"}</span>
        </div>
        <div className="mt-5 h-2 overflow-hidden rounded-full bg-white/8">
          <div className="h-full rounded-full bg-gradient-to-r from-cyanGlow to-purpleGlow" style={{ width: `${assessment.progress_percent ?? 0}%` }} />
        </div>
        <div className="mt-5 flex items-center justify-between">
          <p className="text-xs text-white/40">Closes {new Date(assessment.closes_at).toLocaleDateString()}</p>
          <Link className="btn-secondary" href={actionHref}>
            {assessment.attempt_status === "active" ? "Continue" : assessment.attempt_status === "submitted" ? "Review" : "Start"}
          </Link>
        </div>
      </div>
    </article>
  );
}
