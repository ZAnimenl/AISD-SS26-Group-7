"use client";

import Link from "next/link";
import { Edit3, Loader2, Trash2 } from "lucide-react";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { formatAssessmentStart } from "@/lib/assessmentSchedule";
import type { Assessment } from "@/lib/types";

interface AdminAssessmentTableProps {
  assessments: Assessment[];
  deletingAssessmentId?: string | null;
  onDeleteAssessment?: (assessment: Assessment) => void;
}

export function AdminAssessmentTable({
  assessments,
  deletingAssessmentId = null,
  onDeleteAssessment
}: AdminAssessmentTableProps) {
  return (
    <section className="panel">
      <div className="relative overflow-x-auto">
        <table className="w-full min-w-[980px] text-left text-sm">
          <thead className="text-xs uppercase tracking-[0.14em] text-white/35">
            <tr>
              <th className="pb-3">Assessment</th>
              <th className="pb-3">Status</th>
              <th className="pb-3">Duration</th>
              <th className="pb-3">Starts</th>
              <th className="pb-3">Questions</th>
              <th className="pb-3">AI</th>
              <th className="pb-3">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-white/10">
            {assessments.map((assessment) => (
              <tr key={assessment.assessment_id}>
                <td className="py-4">
                  <p className="font-semibold text-white">{assessment.title}</p>
                  <p className="text-xs text-white/40">{assessment.description}</p>
                </td>
                <td className="py-4"><StatusBadge status={assessment.status} /></td>
                <td className="py-4 text-white/60">{assessment.duration_minutes} min</td>
                <td className="py-4 text-white/60">{formatAssessmentStart(assessment.starts_at)}</td>
                <td className="py-4 text-white/60">{assessment.question_count}</td>
                <td className="py-4 text-white/60">{assessment.ai_enabled ? "Enabled" : "Disabled"}</td>
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
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
