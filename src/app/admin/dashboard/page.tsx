"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3, Brain, FileText, Send, Users } from "lucide-react";
import { AdminAssessmentTable } from "@/components/admin/AdminAssessmentTable";
import { ScoreDonut } from "@/components/reports/ScoreDonut";
import { MetricCard } from "@/components/ui/MetricCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getAdminAssessments, getAdminDashboard, isAuthenticationError } from "@/lib/api";
import type { AdminDashboard, Assessment } from "@/lib/types";

export default function AdminDashboardPage() {
  const router = useRouter();
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [assessments, setAssessments] = useState<Assessment[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [nextDashboard, nextAssessments] = await Promise.all([getAdminDashboard(), getAdminAssessments()]);
        setDashboard(nextDashboard);
        setAssessments(nextAssessments);
      } catch (exception) {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load admin dashboard.");
      }
    }

    load();
  }, [router]);

  if (error) {
    return <SectionHeader eyebrow="Administrator" title={error} />;
  }

  if (!dashboard) {
    return <SectionHeader eyebrow="Administrator" title="Loading dashboard..." />;
  }

  return (
    <div>
      <SectionHeader
        eyebrow="Administrator"
        title="Dashboard"
        action={<Link className="btn-primary" href="/admin/assessments/new">Create assessment</Link>}
      />
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_300px]">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricCard icon={FileText} label="Assessments" value={dashboard.summary.total_assessments} />
          <MetricCard icon={Users} label="Students" value={dashboard.summary.total_students} />
          <MetricCard icon={Send} label="Submissions" value={dashboard.summary.total_submissions} />
          <MetricCard icon={Brain} label="AI events" value={dashboard.summary.ai_interactions} />
        </div>
        <section className="metric-card dynamic-surface flex min-h-48 items-center justify-center border-cyanGlow/25 bg-cyanGlow/8 text-center shadow-[0_0_34px_rgba(0,229,255,0.10)]">
          <div className="relative">
            <ScoreDonut value={dashboard.summary.average_score} size={96} label="Average final score" />
            <p className="mt-4 text-sm font-semibold uppercase tracking-[0.18em] text-white/60">Average final</p>
            <p className="mt-2 text-xs text-white/35">Across completed, fully graded attempts</p>
          </div>
        </section>
      </div>
      <div className="mt-6 grid gap-6 xl:grid-cols-[1.3fr_0.7fr]">
        <AdminAssessmentTable assessments={assessments.slice(0, 3)} compact />
        <section className="panel">
          <h2 className="relative text-lg font-semibold">Recent submissions</h2>
          <div className="relative mt-4 space-y-3">
            {dashboard.recent_submissions.map((submission, index) => (
              <div key={`${submission.student_name}-${submission.assessment_title}-${submission.submitted_at}-${index}`} className="rounded-xl border border-white/10 bg-white/5 p-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="font-semibold">{submission.student_name}</p>
                  <span className="text-cyanGlow">{submission.score}%</span>
                </div>
                <p className="text-sm text-white/45">{submission.assessment_title}</p>
                <p className="mt-2 text-xs text-white/35">{submission.submitted_at}</p>
              </div>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}
