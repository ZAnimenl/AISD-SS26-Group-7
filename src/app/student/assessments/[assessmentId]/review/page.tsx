"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { ArrowLeft, BarChart3, CheckCircle2, FileCode2, Home, RotateCcw } from "lucide-react";
import { getStudentAssessments, getStudentResults, isAuthenticationError } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import type { Assessment } from "@/lib/types";

export default function StudentAssessmentReviewPage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const searchParams = useSearchParams();
  const submissionId = searchParams.get("submissionId");
  const [results, setResults] = useState<Assessment[]>([]);
  const [assessments, setAssessments] = useState<Assessment[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        const [nextResults, nextAssessments] = await Promise.all([
          getStudentResults(),
          getStudentAssessments()
        ]);
        setResults(nextResults);
        setAssessments(nextAssessments);
      } catch (exception) {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setResults([]);
        setAssessments([]);
      } finally {
        setIsLoading(false);
      }
    }

    load();
  }, [router]);

  const result = useMemo(() => {
    const nextResult = submissionId
      ? results.find((item) => item.submission_id === submissionId)
      : results.find((item) => item.assessment_id === assessmentId);
    const assessment = assessments.find((item) => item.assessment_id === assessmentId);

    if (!nextResult) {
      return null;
    }

    return {
      ...nextResult,
      question_count: nextResult.question_count || assessment?.question_count || assessment?.questions.length || 0
    };
  }, [assessmentId, assessments, results, submissionId]);
  const canStartAnotherAttempt = assessments.some((assessment) =>
    assessment.assessment_id === assessmentId
    && assessment.status === "active"
    && assessment.attempt_status !== "expired"
  );

  if (isLoading) {
    return <SectionHeader eyebrow="Assessment review" title="Loading result..." />;
  }

  if (!result) {
    return (
      <div>
        <SectionHeader
          eyebrow="Assessment review"
          title="Result not available yet"
          action={<Link className="btn-secondary" href="/student/results"><ArrowLeft size={16} /> All results</Link>}
        />
        <section className="panel">
          <p className="relative text-white/60">
            The submission was received, but the published result for this assessment is not available in the student results feed yet.
          </p>
          <div className="relative mt-6 flex flex-wrap gap-3">
            <Link className="btn-primary" href="/student/dashboard"><Home size={16} /> Dashboard</Link>
            <Link className="btn-secondary" href="/student/results"><BarChart3 size={16} /> Results</Link>
            {canStartAnotherAttempt ? (
              <Link className="btn-secondary" href={`/student/assessments/${assessmentId}/start`}><RotateCcw size={16} /> Start another attempt</Link>
            ) : null}
          </div>
        </section>
      </div>
    );
  }

  return (
    <div>
      <SectionHeader
        eyebrow="Assessment review"
        title={result.title}
        action={
          <div className="flex flex-wrap justify-end gap-2">
            {canStartAnotherAttempt ? (
              <Link className="btn-primary" href={`/student/assessments/${assessmentId}/start`}><RotateCcw size={16} /> Start another attempt</Link>
            ) : null}
            <Link className="btn-secondary" href="/student/dashboard"><Home size={16} /> Dashboard</Link>
          </div>
        }
      />
      <div className={`grid gap-4 ${result.ai_enabled ? "md:grid-cols-4" : "md:grid-cols-3"}`}>
        <section className="metric-card">
          <div className="relative flex flex-col items-center gap-3 text-center">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow"><CheckCircle2 size={20} /></span>
            <div>
              <p className="text-sm text-white/45">Status</p>
              <StatusBadge status="submitted" />
            </div>
          </div>
        </section>
        <section className="metric-card">
          <div className="relative flex flex-col items-center gap-3 text-center">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow"><BarChart3 size={20} /></span>
            <div>
              <p className="text-sm text-white/45">Functional score</p>
              <p className="text-2xl font-semibold text-cyanGlow">{result.functional_score ?? result.score ?? 0}%</p>
            </div>
          </div>
        </section>
        {result.ai_enabled ? (
          <section className="metric-card">
            <div className="relative flex flex-col items-center gap-3 text-center">
              <span className="grid h-10 w-10 place-items-center rounded-xl bg-purpleGlow/10 text-purpleGlow"><BarChart3 size={20} /></span>
              <div>
                <p className="text-sm text-white/45">AI usage score</p>
                <p className="text-2xl font-semibold text-purpleGlow">
                  {result.ai_usage_score ?? (result.ai_grading_status === "failed" ? "Failed" : "Pending")}
                  {result.ai_usage_score != null ? "%" : ""}
                </p>
              </div>
            </div>
          </section>
        ) : null}
        <section className="metric-card">
          <div className="relative flex flex-col items-center gap-3 text-center">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow"><FileCode2 size={20} /></span>
            <div>
              <p className="text-sm text-white/45">Questions</p>
              <p className="text-2xl font-semibold text-white">{result.question_count}</p>
            </div>
          </div>
        </section>
      </div>
      {result.ai_enabled ? (
        <section className="panel mt-6 text-center">
          <p className="text-sm uppercase tracking-[0.14em] text-white/35">Final score</p>
          <p className="mt-2 text-4xl font-semibold text-cyanGlow">
            {result.final_score ?? "Pending"}{result.final_score != null ? "%" : ""}
          </p>
          <p className="mt-2 text-sm text-white/45">Arithmetic mean of Functional Score and AI Usage Score.</p>
        </section>
      ) : null}
      <section className="panel mt-6">
        <div className="relative">
          <h2 className="text-lg font-semibold">Submission summary</h2>
          <p className="mt-3 max-w-3xl leading-7 text-white/60">
            Your final submission for this assessment has been recorded. Hidden test inputs and expected outputs remain private; only the published score and status are shown here.
          </p>
          {result.ai_enabled && result.reflection_text ? (
            <div className="mt-5 rounded-2xl border border-white/10 bg-white/5 p-4">
              <p className="text-xs uppercase tracking-[0.14em] text-white/35">Your reflection</p>
              <p className="mt-2 leading-7 text-white/65">{result.reflection_text}</p>
            </div>
          ) : null}
          <div className="mt-6 flex flex-wrap gap-3">
            <Link className="btn-primary" href="/student/dashboard"><Home size={16} /> Back to dashboard</Link>
            <Link className="btn-secondary" href="/student/results"><BarChart3 size={16} /> View all results</Link>
            {canStartAnotherAttempt ? (
              <Link className="btn-secondary" href={`/student/assessments/${assessmentId}/start`}><RotateCcw size={16} /> Start another attempt</Link>
            ) : null}
          </div>
        </div>
      </section>
    </div>
  );
}
