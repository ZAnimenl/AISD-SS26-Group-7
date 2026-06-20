"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { AiAssessmentSummary } from "@/components/reports/AiAssessmentSummary";
import { ScoreDonut } from "@/components/reports/ScoreDonut";
import { getAggregateReport, isAuthenticationError, retryAiUsageGrade } from "@/lib/api";
import type { AggregateReport } from "@/lib/types";

function criterion(details: Record<string, unknown>, key: string) {
  const criteria = details.criteria;
  if (!criteria || typeof criteria !== "object") {
    return null;
  }
  const value = (criteria as Record<string, unknown>)[key];
  if (typeof value === "number") return value;
  const pascalKey = key.split("_").map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join("");
  const legacyValue = (criteria as Record<string, unknown>)[pascalKey];
  return typeof legacyValue === "number" ? legacyValue : null;
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
          <div className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-white/10 bg-white/5 p-4">
            <ScoreDonut value={report.average_functional_score} label="Average functional" />
            <p className="text-sm text-white/45">Average functional</p>
          </div>
          {report.ai_enabled ? (
            <>
              <div className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-white/10 bg-white/5 p-4">
                <ScoreDonut value={report.average_ai_usage_score} tone="purple" label="Average AI usage" />
                <p className="text-sm text-white/45">Average AI usage</p>
              </div>
              <div className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-white/10 bg-white/5 p-4">
                <ScoreDonut value={report.average_final_score} label="Average final" />
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
              <div className="flex flex-col items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/5 p-3"><ScoreDonut value={student.functional_score} size={52} label="Functional score" /><p className="text-xs text-white/40">Functional</p></div>
              {report.ai_enabled ? (
                <>
                  <div className="flex flex-col items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/5 p-3">{student.ai_usage_score != null ? <ScoreDonut value={student.ai_usage_score} tone="purple" size={52} label="AI usage score" /> : <p className="text-sm text-purpleGlow">{student.ai_grading.status}</p>}<p className="text-xs text-white/40">AI usage</p></div>
                  <div className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-cyanGlow/35 bg-cyanGlow/10 p-5 shadow-[0_0_28px_rgba(0,229,255,0.10)]">{student.final_score != null ? <ScoreDonut value={student.final_score} size={68} label="Final average score" /> : <p className="text-lg text-cyanGlow">Pending</p>}<p className="text-sm font-medium text-white/65">Final average score</p></div>
                </>
              ) : null}
            </div>
            {report.ai_enabled ? (
              <>
                <AiAssessmentSummary
                  status={student.ai_grading.status}
                  summary={student.ai_grading.summary}
                  reflectionText={student.reflection.text}
                  details={student.ai_grading.details}
                  interactionCount={student.ai_usage_summary.total_interactions}
                  totalTokens={student.ai_usage_summary.total_tokens}
                  confidence={student.ai_grading.confidence}
                />
                <div className="relative mt-4 grid gap-4 lg:grid-cols-2">
                <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <p className="text-xs uppercase tracking-[0.14em] text-white/35">Rubric breakdown</p>
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
                  <div className="mt-4 space-y-2 text-sm">
                    <p>Prompt quality and context <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "prompt_quality_and_context") ?? "—"}/30</span></p>
                    <div className="rounded-lg border border-white/10 bg-white/[0.03] p-2">
                      <p className="font-medium text-white/75">Token and interaction efficiency <span className="float-right text-cyanGlow">{(criterion(student.ai_grading.details, "behavioral_efficiency") ?? 0) + (criterion(student.ai_grading.details, "objective_repetition") ?? 0)}/40</span></p>
                      <p className="mt-1 pl-3 text-xs text-white/50">Behavioral efficiency <span className="float-right">{criterion(student.ai_grading.details, "behavioral_efficiency") ?? "—"}/30</span></p>
                      <p className="mt-1 pl-3 text-xs text-white/50">Objective repetition <span className="float-right">{criterion(student.ai_grading.details, "objective_repetition") ?? "—"}/10</span></p>
                    </div>
                    <p>Critical evaluation and adaptation <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "critical_evaluation_and_adaptation") ?? "—"}/20</span></p>
                    <p>Reflection quality and consistency <span className="float-right text-cyanGlow">{criterion(student.ai_grading.details, "reflection_quality_and_consistency") ?? "—"}/10</span></p>
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
              </>
            ) : null}
          </section>
        ))}
      </div>
    </div>
  );
}
