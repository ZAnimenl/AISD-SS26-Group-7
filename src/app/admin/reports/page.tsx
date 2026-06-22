"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3 } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { ScoreBar } from "@/components/reports/ScoreBar";
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
      <div className="grid gap-4 xl:grid-cols-2">
        {reports.map((report) => (
          <Link key={report.assessment_id} href={`/admin/reports/${report.assessment_id}`} className="panel block transition hover:bg-white/5">
            <div className="relative">
              <BarChart3 className="text-cyanGlow" />
              <h2 className="mt-4 text-xl font-semibold">{report.assessment_title}</h2>
              {report.ai_enabled ? (
                <div className="mt-5 flex items-center justify-center gap-5 rounded-2xl border border-cyanGlow/30 bg-cyanGlow/10 p-5 shadow-[0_0_28px_rgba(0,229,255,0.10)]">
                  <ScoreDonut value={report.average_final_score} size={82} label="Final average" />
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-cyanGlow/80">Final score</p>
                    <p className="mt-2 text-lg font-semibold text-white">Overall assessment result</p>
                  </div>
                </div>
              ) : null}
              <div className="mt-5 grid gap-3 text-sm sm:grid-cols-2">
                <div className="min-h-28 rounded-xl border border-white/10 bg-white/5 p-4">
                  <span className="text-white/45">Functional</span>
                  <div className="mt-3"><ScoreBar value={report.average_functional_score} label="Functional average" /></div>
                </div>
                {report.ai_enabled ? (
                  <div className="min-h-28 rounded-xl border border-white/10 bg-white/5 p-4">
                    <span className="text-white/45">AI usage</span>
                    <div className="mt-3"><ScoreBar value={report.average_ai_usage_score} tone="purple" label="AI usage average" /></div>
                  </div>
                ) : null}
                <div className="flex min-h-24 flex-col items-center justify-center rounded-xl border border-white/10 bg-white/5 p-3 text-center">
                  <span className="block text-2xl text-purpleGlow">{report.completion_count}/{report.participant_count}</span>
                  <span className="text-white/45">Completed</span>
                </div>
                <div className="flex min-h-24 flex-col items-center justify-center rounded-xl border border-white/10 bg-white/5 p-3 text-center">
                  <span className="block text-2xl text-cyanGlow">{report.ai_interactions}</span>
                  <span className="text-white/45">AI interactions</span>
                </div>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
