import Link from "next/link";
import { BarChart3 } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getReportList } from "@/lib/mock-api";

export default function ReportsPage() {
  const reports = getReportList();

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
                <p className="rounded-xl border border-white/10 bg-white/5 p-3"><span className="block text-2xl text-cyanGlow">{report.average_score}%</span><span className="text-white/45">Average</span></p>
                <p className="rounded-xl border border-white/10 bg-white/5 p-3"><span className="block text-2xl text-purpleGlow">{report.completion_count}/{report.participant_count}</span><span className="text-white/45">Completed</span></p>
              </div>
              <p className="mt-4 text-sm text-white/45">{report.ai_interactions} AI interactions summarized</p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
