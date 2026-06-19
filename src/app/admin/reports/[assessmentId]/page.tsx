"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAggregateReport, isAuthenticationError } from "@/lib/api";
import type { AggregateReport, TokenEfficiencyIndicator } from "@/lib/types";

const EFFICIENCY_LABELS: Record<TokenEfficiencyIndicator, string> = {
  no_ai_usage: "No AI usage",
  strategic: "Strategic",
  token_heavy_success: "Token-heavy success",
  inefficient: "Inefficient",
  needs_review: "Needs review"
};

function formatEfficiency(value: TokenEfficiencyIndicator) {
  return EFFICIENCY_LABELS[value] ?? "Needs review";
}

export default function ReportDetailPage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [report, setReport] = useState<AggregateReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAggregateReport(assessmentId).then(setReport).catch((exception) => {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to load report.");
    });
  }, [assessmentId, router]);

  if (error) {
    return <SectionHeader eyebrow="Report detail" title={error} />;
  }

  if (!report) {
    return <SectionHeader eyebrow="Report detail" title="Connecting to backend..." />;
  }

  const max = Math.max(1, ...report.score_distribution.map((item) => item.count));

  return (
    <div>
      <SectionHeader eyebrow="Report detail" title={report.assessment_title} />
      <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <section className="panel">
          <div className="relative">
            <h2 className="text-lg font-semibold">Aggregate metrics</h2>
            <div className="mt-4 grid grid-cols-2 gap-3">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center"><p className="text-3xl text-cyanGlow">{report.average_score}%</p><p className="text-sm text-white/45">Average score</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center"><p className="text-3xl text-purpleGlow">{report.completion_count}/{report.participant_count}</p><p className="text-sm text-white/45">Completed</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center"><p className="text-3xl text-cyanGlow">{report.ai_usage_summary.total_tokens.toLocaleString()}</p><p className="text-sm text-white/45">AI tokens</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center"><p className="text-3xl text-purpleGlow">{report.ai_usage_summary.average_tokens_per_interaction}</p><p className="text-sm text-white/45">Avg tokens / interaction</p></div>
            </div>
            <div className="mt-4 rounded-2xl border border-white/10 bg-black/20 p-4 text-center">
              <p className="text-xs uppercase tracking-[0.14em] text-white/35">Assessment token efficiency</p>
              <p className="mt-2 text-lg font-semibold text-cyanGlow">{formatEfficiency(report.ai_usage_summary.token_efficiency_indicator)}</p>
              <p className="mt-1 text-sm text-white/45">
                {report.ai_usage_summary.total_interactions} interactions across {report.ai_usage_summary.per_task_token_totals.length} tasks
              </p>
            </div>
            <h3 className="mt-6 text-sm font-semibold text-white/80">Score distribution</h3>
            <div className="mt-3 space-y-3">
              {report.score_distribution.map((bucket) => (
                <div key={bucket.range}>
                  <div className="mb-1 flex justify-between text-xs text-white/45"><span>{bucket.range}</span><span>{bucket.count}</span></div>
                  <div className="h-3 rounded-full bg-white/8"><div className="h-full rounded-full bg-gradient-to-r from-purpleGlow to-cyanGlow" style={{ width: `${(bucket.count / max) * 100}%` }} /></div>
                </div>
              ))}
            </div>
          </div>
        </section>
        <section className="panel">
          <div className="relative overflow-x-auto">
            <table className="w-full min-w-[920px] text-left text-sm">
              <thead className="text-xs uppercase tracking-[0.14em] text-white/35">
                <tr><th className="pb-3">Student</th><th className="pb-3">Status</th><th className="pb-3">Submission</th><th className="pb-3">Score</th><th className="pb-3">AI usage</th><th className="pb-3">Avg tokens</th><th className="pb-3">Per-task tokens</th></tr>
              </thead>
              <tbody className="divide-y divide-white/10">
                {report.students.map((student) => (
                  <tr key={student.attempt_id}>
                    <td className="py-4"><p className="font-semibold">{student.student_name}</p><p className="text-xs text-white/40">{student.student_email}</p></td>
                    <td className="py-4"><StatusBadge status={student.attempt_status} /></td>
                    <td className="py-4"><StatusBadge status={student.submission_status} /></td>
                    <td className="py-4 text-cyanGlow">{student.score}/{student.max_score}</td>
                    <td className="py-4 text-white/55">
                      <p>{student.ai_usage_summary.total_interactions} interactions · {student.ai_usage_summary.total_tokens.toLocaleString()} tokens</p>
                      <p className="mt-1 text-xs text-cyanGlow">{formatEfficiency(student.ai_usage_summary.token_efficiency_indicator)}</p>
                      <p className="mt-1 text-xs text-white/35">{student.ai_usage_summary.main_semantic_tags.join(", ") || "No semantic tags"}</p>
                    </td>
                    <td className="py-4 font-mono text-sm text-purpleGlow">{student.ai_usage_summary.average_tokens_per_interaction}</td>
                    <td className="py-4 text-xs text-white/45">
                      <div className="space-y-1">
                        {student.ai_usage_summary.per_task_token_totals.length ? student.ai_usage_summary.per_task_token_totals.map((task) => (
                          <p key={task.question_id} className="max-w-[220px] truncate" title={`${task.task_title}: ${task.total_tokens} tokens`}>
                            {task.task_title}: {task.total_tokens.toLocaleString()}
                          </p>
                        )) : <p>No per-task usage</p>}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  );
}
