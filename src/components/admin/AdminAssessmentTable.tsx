"use client";

import Link from "next/link";
import { Edit3, Loader2, Trash2 } from "lucide-react";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { effectiveAssessmentStatus, formatAssessmentExpiry, formatAssessmentStart } from "@/lib/assessmentSchedule";
import type { Assessment } from "@/lib/types";

interface AdminAssessmentTableProps {
  assessments: Assessment[];
  compact?: boolean;
  deletingAssessmentId?: string | null;
  onDeleteAssessment?: (assessment: Assessment) => void;
}

export function AdminAssessmentTable({
  assessments,
  compact = false,
  deletingAssessmentId = null,
  onDeleteAssessment
}: AdminAssessmentTableProps) {
  const table = (
    <table className={`w-full text-left text-sm ${compact ? "table-fixed" : "min-w-[980px]"}`}>
      <thead className="text-xs uppercase tracking-[0.14em] text-white/35">
        <tr>
          <th className={compact ? "w-[28%] pb-3 pr-3" : "pb-3"}>Assessment</th>
          <th className={compact ? "w-[14%] pb-3 pr-3" : "pb-3"}>Status</th>
          <th className={compact ? "w-[13%] pb-3 pr-3" : "pb-3"}>Duration</th>
          <th className={compact ? "w-[22.5%] pb-3 pr-3" : "pb-3"}>Starts</th>
          <th className={compact ? "w-[22.5%] pb-3" : "pb-3"}>Expires</th>
          {!compact ? <th className="pb-3">Questions</th> : null}
          {!compact ? <th className="pb-3">AI</th> : null}
          {!compact ? <th className="pb-3">Action</th> : null}
        </tr>
      </thead>
      <tbody className="divide-y divide-white/10">
        {assessments.map((assessment) => (
          <tr key={assessment.assessment_id}>
            <td className={compact ? "min-w-0 py-4 pr-3" : "py-4"}>
              <p className="truncate font-semibold text-white">{assessment.title}</p>
              <p className="truncate text-xs text-white/40">{assessment.description}</p>
            </td>
            <td className={compact ? "py-4 pr-3" : "py-4"}><StatusBadge status={effectiveAssessmentStatus(assessment)} /></td>
            <td className={compact ? "py-4 pr-3 text-white/60" : "py-4 text-white/60"}>{assessment.duration_minutes} min</td>
            <td className={compact ? "py-4 pr-3 text-xs leading-5 text-white/60" : "py-4 text-white/60"}>{formatAssessmentStart(assessment.starts_at)}</td>
            <td className={compact ? "py-4 text-xs leading-5 text-white/60" : "py-4 text-white/60"}>{formatAssessmentExpiry(assessment.expires_at)}</td>
            {!compact ? <td className="py-4 text-white/60">{assessment.question_count}</td> : null}
            {!compact ? <td className="py-4 text-white/60">{assessment.ai_enabled ? "Enabled" : "Disabled"}</td> : null}
            {!compact ? (
              <td className="py-4">
                <div className="flex flex-wrap gap-2">
                  <Link className="btn-secondary px-3 py-2" href={`/admin/assessments/${assessment.assessment_id}`}>
                    <Edit3 size={15} />
                    Edit
                  </Link>
                  {onDeleteAssessment ? (
                    <button
                      className="btn-secondary px-3 py-2 text-pinkGlow"
                      type="button"
                      disabled={deletingAssessmentId !== null}
                      onClick={() => onDeleteAssessment(assessment)}
                    >
                      {deletingAssessmentId === assessment.assessment_id ? <Loader2 className="animate-spin" size={15} /> : <Trash2 size={15} />}
                      {deletingAssessmentId === assessment.assessment_id ? "Deleting..." : "Delete"}
                    </button>
                  ) : null}
                </div>
              </td>
            ) : null}
          </tr>
        ))}
      </tbody>
    </table>
  );

  return (
    <section className="panel">
      {compact ? <div className="relative min-w-0">{table}</div> : <div className="scrollbar-soft relative overflow-x-auto">{table}</div>}
    </section>
  );
}
