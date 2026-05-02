"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3, CheckCircle2, Clock, FileCode2 } from "lucide-react";
import { AssessmentCard } from "@/components/student/AssessmentCard";
import { MetricCard } from "@/components/ui/MetricCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getStudentAssessments, getStudentDashboard, getStudentResults } from "@/lib/api";
import type { Assessment, StudentDashboard } from "@/lib/types";

export default function StudentDashboardPage() {
  const router = useRouter();
  const [dashboard, setDashboard] = useState<StudentDashboard | null>(null);
  const [assessments, setAssessments] = useState<Assessment[]>([]);
  const [results, setResults] = useState<Assessment[]>([]);

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
      } catch {
        router.replace("/login");
      }
    }

    load();
  }, [router]);

  if (!dashboard) {
    return <SectionHeader eyebrow="Student" title="Connecting to backend..." />;
  }

  return (
    <div>
      <SectionHeader
        eyebrow="Student"
        title="Dashboard"
        action={<Link className="btn-primary" href="/student/assessments">Open assessments</Link>}
      />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={FileCode2} label="Available" value={dashboard.summary.available_assessments} detail="Ready to start" />
        <MetricCard icon={Clock} label="In progress" value={dashboard.summary.in_progress_attempts} detail="Active backend sessions" />
        <MetricCard icon={CheckCircle2} label="Completed" value={dashboard.summary.completed_assessments} detail="Submitted assessments" />
        <MetricCard icon={BarChart3} label="Average score" value={`${dashboard.summary.average_score}%`} detail="Published results" />
      </div>
      <div className="mt-6 grid gap-6 xl:grid-cols-[1.4fr_0.8fr]">
        <div>
          <h2 className="mb-4 text-lg font-semibold">Available and active assessments</h2>
          <div className="grid gap-4 lg:grid-cols-2">
            {assessments.slice(0, 2).map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
          </div>
        </div>
        <aside className="space-y-6">
          <section className="panel">
            <h2 className="relative text-lg font-semibold">Recent activity</h2>
            <div className="relative mt-4 space-y-3">
              {dashboard.recent_activity.map((item) => (
                <div key={item.label} className="rounded-xl border border-white/10 bg-white/5 p-3">
                  <p className="text-sm text-white">{item.label}</p>
                  <p className="text-xs text-white/45">{item.detail}</p>
                  <p className="mt-2 text-xs text-cyanGlow/70">{item.timestamp}</p>
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
