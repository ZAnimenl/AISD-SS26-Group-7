"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { CheckCircle2, Clock, FileCode2 } from "lucide-react";
import { AssessmentCard } from "@/components/student/AssessmentCard";
import { ScoreDonut } from "@/components/reports/ScoreDonut";
import { MetricCard } from "@/components/ui/MetricCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getStudentAssessments, getStudentDashboard, getStudentResults, isAuthenticationError } from "@/lib/api";
import { partitionAssessments } from "@/lib/assessmentSchedule";
import type { Assessment, StudentDashboard } from "@/lib/types";

export default function StudentDashboardPage() {
  const router = useRouter();
  const [dashboard, setDashboard] = useState<StudentDashboard | null>(null);
  const [assessments, setAssessments] = useState<Assessment[]>([]);
  const [results, setResults] = useState<Assessment[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [nextDashboard, nextAssessments, nextResults] = await Promise.all([
          getStudentDashboard(),
          getStudentAssessments(),
          getStudentResults()
        ]);
        setDashboard(nextDashboard);
        setAssessments(nextAssessments);
        setResults(nextResults);
      } catch (exception) {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load student dashboard.");
      }
    }

    load();
  }, [router]);

  if (error) {
    return <SectionHeader eyebrow="Student" title={error} />;
  }

  if (!dashboard) {
    return <SectionHeader eyebrow="Student" title="Loading dashboard..." />;
  }

  const { available: availableAssessments, other: otherAssessments } = partitionAssessments(assessments);

  return (
    <div>
      <SectionHeader
        eyebrow="Student"
        title="Dashboard"
        action={<Link className="btn-primary" href="/student/assessments">Open assessments</Link>}
      />
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_300px]">
        <div className="grid gap-4 sm:grid-cols-3">
          <MetricCard icon={FileCode2} label="Available" value={dashboard.summary.available_assessments} />
          <MetricCard icon={Clock} label="In progress" value={dashboard.summary.in_progress_attempts} />
          <MetricCard icon={CheckCircle2} label="Completed" value={dashboard.summary.completed_assessments} />
        </div>
        <section className="metric-card dynamic-surface flex min-h-48 items-center justify-center border-cyanGlow/25 bg-cyanGlow/8 text-center shadow-[0_0_34px_rgba(0,229,255,0.10)]">
          <div className="relative">
            <ScoreDonut value={dashboard.summary.average_score} size={96} label="Average score" />
            <p className="mt-4 text-sm font-semibold uppercase tracking-[0.18em] text-white/60">Average score</p>
            <p className="mt-2 text-xs text-white/35">Across your completed assessments</p>
          </div>
        </section>
      </div>
      <div className="mt-6 grid gap-6 xl:grid-cols-[1.4fr_0.8fr]">
        <div>
          <h2 className="mb-4 text-lg font-semibold">Active assessments</h2>
          <div className="grid gap-4 lg:grid-cols-2">
            {availableAssessments.slice(0, 2).map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
            {availableAssessments.length === 0 ? (
              <section className="panel text-sm text-white/55 lg:col-span-2">No assessments are available to start or continue right now.</section>
            ) : null}
          </div>
          <h2 className="mb-4 mt-6 text-lg font-semibold">Other assessments</h2>
          <div className="grid gap-4 lg:grid-cols-2">
            {otherAssessments.slice(0, 2).map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
            {otherAssessments.length === 0 ? (
              <section className="panel text-sm text-white/55 lg:col-span-2">No scheduled or closed assessments.</section>
            ) : null}
          </div>
        </div>
        <aside className="space-y-6">
          <section className="panel">
            <h2 className="relative text-lg font-semibold">Recent activity</h2>
            <div className="relative mt-4 space-y-3">
              {dashboard.recent_activity.map((item, index) => (
                <div key={`${item.timestamp}-${item.label}-${index}`} className="rounded-xl border border-white/10 bg-white/5 p-3">
                  <p className="text-sm text-white">{item.label}</p>
                  <p className="text-xs text-white/45">{item.detail}</p>
                  <p className="mt-2 text-xs text-cyanGlow/70">{formatDateTime(item.timestamp)}</p>
                </div>
              ))}
            </div>
          </section>
          <section className="panel">
            <h2 className="relative text-lg font-semibold">Results preview</h2>
            {results.map((result) => (
              <div key={result.assessment_id} className="relative mt-4 flex items-center justify-between rounded-xl border border-white/10 bg-black/20 p-3">
                <span className="text-sm text-white/80">{result.title}</span>
                <span className="text-lg font-semibold text-cyanGlow">{result.score}%</span>
              </div>
            ))}
          </section>
        </aside>
      </div>
    </div>
  );
}

function formatDateTime(timestamp: string) {
  if (!timestamp) return "";
  try {
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) return timestamp;
    return date.toLocaleString("en-US", {
      dateStyle: "medium",
      timeStyle: "medium"
    });
  } catch {
    return timestamp;
  }
}
