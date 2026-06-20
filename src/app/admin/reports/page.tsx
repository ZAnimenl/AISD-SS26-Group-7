"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3 } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { ScoreDonut } from "@/components/reports/ScoreDonut";
import { getReportList, isAuthenticationError } from "@/lib/api";
import type { ReportListItem } from "@/lib/types";

export default function ReportsPage() {
  const router = useRouter();
  const [reports, setReports] = useState<ReportListItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getReportList().then(setReports).catch((exception) => {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to load reports.");
    });
  }, [router]);

  if (error) {
    return <SectionHeader eyebrow="Administrator" title={error} />;
  }

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Reports" />
      <div className="grid gap-4 lg:grid-cols-3">
        {reports.map((report) => (
          <Link key={report.assessment_id} href={`/admin/reports/${report.assessment_id}`} className="panel block transition hover:bg-white/5">
            <div className="relative">
              <BarChart3 className="text-cyanGlow" />
              <h2 className="mt-4 text-xl font-semibold">{report.assessment_title}</h2>
              <div className="mt-5 grid grid-cols-2 gap-3 text-sm">
                <div className="flex flex-col items-center gap-2 rounded-xl border border-white/10 bg-white/5 p-3"><ScoreDonut value={report.average_functional_score} size={48} label="Functional average" /><span className="text-white/45">Functional</span></div>
                <p className="rounded-xl border border-white/10 bg-white/5 p-3"><span className="block text-2xl text-purpleGlow">{report.completion_count}/{report.participant_count}</span><span className="text-white/45">Completed</span></p>
                {report.ai_enabled ? <div className="flex flex-col items-center gap-2 rounded-xl border border-white/10 bg-white/5 p-3"><ScoreDonut value={report.average_ai_usage_score} tone="purple" size={48} label="AI usage average" /><span className="text-white/45">AI usage</span></div> : null}
                {report.ai_enabled ? <div className="flex flex-col items-center gap-2 rounded-xl border border-white/10 bg-white/5 p-3"><ScoreDonut value={report.average_final_score} size={48} label="Final average" /><span className="text-white/45">Final</span></div> : null}
              </div>
              <p className="mt-4 text-sm text-white/45">
                {report.ai_interactions} AI interactions
              </p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
