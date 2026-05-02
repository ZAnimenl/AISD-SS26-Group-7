"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3, Brain, FileText, Send, Users } from "lucide-react";
import { AdminAssessmentTable } from "@/components/admin/AdminAssessmentTable";
import { MetricCard } from "@/components/ui/MetricCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getAdminAssessments, getAdminDashboard } from "@/lib/api";
import type { AdminDashboard, Assessment } from "@/lib/types";

export default function AdminDashboardPage() {
  const router = useRouter();
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [assessments, setAssessments] = useState<Assessment[]>([]);

  useEffect(() => {
    async function load() {
      try {
        const [nextDashboard, nextAssessments] = await Promise.all([getAdminDashboard(), getAdminAssessments()]);
        setDashboard(nextDashboard);
        setAssessments(nextAssessments);
      } catch {
        router.replace("/login");
      }
    }

    load();
  }, [router]);

  if (!dashboard) {
    return <SectionHeader eyebrow="Administrator" title="Connecting to backend..." />;
  }

  return (
    <div>
      <SectionHeader
        eyebrow="Administrator"
        title="Dashboard"
        action={<Link className="btn-primary" href="/admin/assessments/new">Create assessment</Link>}
      />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        <MetricCard icon={FileText} label="Assessments" value={dashboard.summary.total_assessments} detail={`${dashboard.summary.active_assessments} active`} />
        <MetricCard icon={Users} label="Students" value={dashboard.summary.total_students} detail="Backend users" />
        <MetricCard icon={Send} label="Submissions" value={dashboard.summary.total_submissions} detail="All assessments" />
        <MetricCard icon={BarChart3} label="Average" value={`${dashboard.summary.average_score}%`} detail="Latest submissions" />
        <MetricCard icon={Brain} label="AI events" value={dashboard.summary.ai_interactions} detail="Usage summaries" />
      </div>
      <div className="mt-6 grid gap-6 xl:grid-cols-[1.3fr_0.7fr]">
        <AdminAssessmentTable assessments={assessments.slice(0, 3)} />
        <section className="panel">
          <h2 className="relative text-lg font-semibold">Recent submissions</h2>
          <div className="relative mt-4 space-y-3">
            {dashboard.recent_submissions.map((submission) => (
              <div key={`${submission.student_name}-${submission.assessment_title}`} className="rounded-xl border border-white/10 bg-white/5 p-4">
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
