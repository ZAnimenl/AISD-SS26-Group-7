import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAggregateReport } from "@/lib/mock-api";

export default function ReportDetailPage({ params }: { params: { assessmentId: string } }) {
  const report = getAggregateReport(params.assessmentId);
  const max = Math.max(...report.score_distribution.map((item) => item.count));

  return (
    <div>
      <SectionHeader eyebrow="Report detail" title={report.assessment_title} />
      <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <section className="panel">
          <div className="relative">
            <h2 className="text-lg font-semibold">Aggregate metrics</h2>
            <div className="mt-4 grid grid-cols-2 gap-3">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><p className="text-3xl text-cyanGlow">{report.average_score}%</p><p className="text-sm text-white/45">Average score</p></div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><p className="text-3xl text-purpleGlow">{report.completion_count}/{report.participant_count}</p><p className="text-sm text-white/45">Completed</p></div>
            </div>
            <h3 className="mt-6 text-sm font-semibold text-white/80">Score distribution</h3>
            <div className="mt-3 space-y-3">
              {report.score_distribution.map((bucket) => (
                <div key={bucket.range}>
                  <div className="mb-1 flex justify-between text-xs text-white/45"><span>{bucket.range}</span><span>{bucket.count}</span></div>
                  <div className="h-3 rounded-full bg-white/8"><div className="h-full rounded-full bg-gradient-to-r from-purpleGlow to-cyanGlow" style={{ width: `${(bucket.count / max) * 100}%` }} /></div>
                </div>
              ))}
            </div>
          </div>
        </section>
        <section className="panel">
          <div className="relative overflow-x-auto">
            <table className="w-full min-w-[820px] text-left text-sm">
              <thead className="text-xs uppercase tracking-[0.14em] text-white/35">
                <tr><th className="pb-3">Student</th><th className="pb-3">Status</th><th className="pb-3">Submission</th><th className="pb-3">Score</th><th className="pb-3">AI usage</th></tr>
              </thead>
              <tbody className="divide-y divide-white/10">
                {report.students.map((student) => (
                  <tr key={student.user_id}>
                    <td className="py-4"><p className="font-semibold">{student.student_name}</p><p className="text-xs text-white/40">{student.student_email}</p></td>
                    <td className="py-4"><StatusBadge status={student.attempt_status} /></td>
                    <td className="py-4"><StatusBadge status={student.submission_status} /></td>
                    <td className="py-4 text-cyanGlow">{student.score}/{student.max_score}</td>
                    <td className="py-4 text-white/55">{student.ai_usage_summary.total_interactions} interactions · {student.ai_usage_summary.main_semantic_tags.join(", ")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  );
}
