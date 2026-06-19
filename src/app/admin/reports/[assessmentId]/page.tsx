"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAggregateReport, isAuthenticationError, retryAiUsageGrade } from "@/lib/api";
import type { AggregateReport } from "@/lib/types";

function criterion(details: Record<string, unknown>, key: string) {
  const criteria = details.criteria;
  if (!criteria || typeof criteria !== "object") {
    return null;
  }
  const value = (criteria as Record<string, unknown>)[key];
  return typeof value === "number" ? value : null;
}

export default function ReportDetailPage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [report, setReport] = useState<AggregateReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retryingStudentId, setRetryingStudentId] = useState<string | null>(null);

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
      <section className="panel">
        <div className={`relative grid gap-3 ${report.ai_enabled ? "md:grid-cols-3" : "md:grid-cols-2"}`}>
          <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center">
            <p className="text-3xl text-cyanGlow">{report.average_functional_score}%</p>
            <p className="text-sm text-white/45">Average functional</p>
          </div>
          {report.ai_enabled ? (
            <>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center">
                <p className="text-3xl text-purpleGlow">{report.average_ai_usage_score}%</p>
                <p className="text-sm text-white/45">Average AI usage</p>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center">
                <p className="text-3xl text-cyanGlow">{report.average_final_score}%</p>
                <p className="text-sm text-white/45">Average final</p>
              </div>
            </>
          ) : null}
          <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center">
            <p className="text-3xl text-purpleGlow">{report.completion_count}/{report.participant_count}</p>
            <p className="text-sm text-white/45">Completed</p>
          </div>
          {report.ai_enabled ? (
            <>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center">
                <p className="text-3xl text-cyanGlow">{report.ai_usage_summary.total_tokens.toLocaleString()}</p>
                <p className="text-sm text-white/45">Descriptive AI tokens</p>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-center">
                <p className="text-3xl text-purpleGlow">{report.ai_usage_summary.total_interactions}</p>
                <p className="text-sm text-white/45">AI interactions</p>
              </div>
            </>
          ) : null}
        </div>
        <h3 className="relative mt-6 text-sm font-semibold text-white/80">Functional score distribution</h3>
        <div className="relative mt-3 space-y-3">
          {report.score_distribution.map((bucket) => (
            <div key={bucket.range}>
              <div className="mb-1 flex justify-between text-xs text-white/45"><span>{bucket.range}</span><span>{bucket.count}</span></div>
              <div className="h-3 rounded-full bg-white/8"><div className="h-full rounded-full bg-gradient-to-r from-purpleGlow to-cyanGlow" style={{ width: `${(bucket.count / max) * 100}%` }} /></div>
            </div>
          ))}
        </div>
      </section>

      <div className="mt-6 space-y-5">
        {report.students.map((student) => (
          <section key={student.attempt_id} className="panel">
            <div className="relative flex flex-wrap items-start justify-between gap-4">
              <div>
                <h2 className="text-xl font-semibold">{student.student_name}</h2>
                <p className="text-sm text-white/40">{student.student_email}</p>
              </div>
              <div className="flex gap-2"><StatusBadge status={student.attempt_status} /><StatusBadge status={student.submission_status} /></div>
            </div>
            <div className={`relative mt-5 grid gap-3 ${report.ai_enabled ? "sm:grid-cols-3" : "sm:grid-cols-1"}`}>
              <div className="rounded-xl border border-white/10 bg-white/5 p-3 text-center"><p className="text-2xl text-cyanGlow">{student.functional_score}%</p><p className="text-xs text-white/40">Functional</p></div>
              {report.ai_enabled ? (
                <>
                  <div className="rounded-xl border border-white/10 bg-white/5 p-3 text-center"><p className="text-2xl text-purpleGlow">{student.ai_usage_score ?? student.ai_grading.status}</p><p className="text-xs text-white/40">AI usage</p></div>
                  <div className="rounded-xl border border-white/10 bg-white/5 p-3 text-center"><p className="text-2xl text-cyanGlow">{student.final_score ?? "Pending"}</p><p className="text-xs text-white/40">Final average</p></div>
                </>
              ) : null}
            </div>
            {report.ai_enabled ? (
              <div className="relative mt-5 grid gap-4 lg:grid-cols-2">
                <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <p className="text-xs uppercase tracking-[0.14em] text-white/35">AI grading evidence</p>
                  <p className="mt-2 text-sm text-white/65">{student.ai_grading.summary ?? "Automatic grading has not completed."}</p>
                  {student.ai_grading.status === "failed" ? (
                    <button
                      className="btn-secondary mt-3 px-3 py-2 text-xs"
                      disabled={retryingStudentId === student.user_id}
                      onClick={async () => {
                        setRetryingStudentId(student.user_id);
                        try {
                          await retryAiUsageGrade(assessmentId, student.user_id);
                          setReport(await getAggregateReport(assessmentId));
                        } catch (exception) {
                          setError(exception instanceof Error ? exception.message : "AI grading retry failed.");
                        } finally {
                          setRetryingStudentId(null);
                        }
                      }}
                    >
                      {retryingStudentId === student.user_id ? "Retrying..." : "Retry AI grading"}
                    </button>
                  ) : null}
                  <div className="mt-4 grid grid-cols-2 gap-2 text-sm">
                    <p>Prompt quality <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "prompt_quality_and_context") ?? "—"}/30</span></p>
                    <p>Behavioral efficiency <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "behavioral_efficiency") ?? "—"}/30</span></p>
                    <p>Objective repetition <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "objective_repetition") ?? "—"}/10</span></p>
                    <p>Critical evaluation <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "critical_evaluation_and_adaptation") ?? "—"}/20</span></p>
                    <p>Reflection <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "reflection_quality_and_consistency") ?? "—"}/10</span></p>
                  </div>
                  <p className="mt-4 text-xs text-white/35">{student.ai_usage_summary.total_interactions} interactions · {student.ai_usage_summary.total_tokens.toLocaleString()} tokens · confidence {student.ai_grading.confidence ?? "—"}</p>
                </div>
                <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <p className="text-xs uppercase tracking-[0.14em] text-white/35">Student reflection</p>
                  <p className="mt-2 leading-7 text-white/65">{student.reflection.text || "No reflection text submitted."}</p>
                  <p className="mt-3 text-xs text-white/35">{student.reflection.word_count} words · {student.reflection.submitted_by ?? "not submitted"}</p>
                  <div className="mt-4 space-y-1 text-xs text-white/45">
                    {student.ai_usage_summary.per_task_token_totals.map((task) => (
                      <p key={task.question_id}>{task.task_title}: {task.total_tokens.toLocaleString()} tokens</p>
                    ))}
                  </div>
                </div>
              </div>
            ) : null}
          </section>
        ))}
      </div>
    </div>
  );
}
