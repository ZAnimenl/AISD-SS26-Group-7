import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { updateAssessmentMock } from "@/lib/mock-api";

export default function EditAssessmentPage({ params }: { params: { assessmentId: string } }) {
  const assessment = updateAssessmentMock(params.assessmentId);
  const questions = assessment.questions.length ? assessment.questions : updateAssessmentMock("algorithms-2026").questions;

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title={`Edit ${assessment.title}`} />
      <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <section className="panel">
          <div className="relative grid gap-4">
            <div className="flex items-center justify-between"><h2 className="text-lg font-semibold">Assessment details</h2><StatusBadge status={assessment.status} /></div>
            <label className="grid gap-2 text-sm text-white/60">Title<input className="field" defaultValue={assessment.title} /></label>
            <label className="grid gap-2 text-sm text-white/60">Description<textarea className="field min-h-28" defaultValue={assessment.description} /></label>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="grid gap-2 text-sm text-white/60">Duration<input className="field" type="number" defaultValue={assessment.duration_minutes} /></label>
              <label className="grid gap-2 text-sm text-white/60">Status<select className="field" defaultValue={assessment.status}><option>draft</option><option>active</option><option>closed</option><option>archived</option></select></label>
            </div>
            <button className="btn-primary w-fit">Save mock changes</button>
          </div>
        </section>
        <section className="panel">
          <div className="relative">
            <h2 className="text-lg font-semibold">Questions and test cases</h2>
            <div className="mt-4 space-y-4">
              {questions.map((question) => (
                <article key={question.question_id} className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question editor</p>
                      <h3 className="mt-1 text-lg font-semibold">{question.title}</h3>
                      <p className="mt-2 text-sm text-white/50">{question.problem_description_markdown}</p>
                    </div>
                    <span className="badge">Python + JavaScript</span>
                  </div>
                  <div className="mt-4 grid gap-3 lg:grid-cols-2">
                    <textarea className="field min-h-28 font-mono" defaultValue={question.starter_code.python} />
                    <textarea className="field min-h-28 font-mono" defaultValue={question.starter_code.javascript} />
                  </div>
                  <div className="mt-4 overflow-x-auto">
                    <table className="w-full min-w-[620px] text-left text-xs">
                      <thead className="uppercase tracking-[0.14em] text-white/35">
                        <tr><th className="pb-2">Name</th><th className="pb-2">Visibility</th><th className="pb-2">Input preview</th><th className="pb-2">Expected preview</th><th className="pb-2">Points</th></tr>
                      </thead>
                      <tbody className="divide-y divide-white/10">
                        {question.admin_test_cases?.map((testCase) => (
                          <tr key={testCase.test_case_id}>
                            <td className="py-3 text-white/80">{testCase.name}</td>
                            <td className="py-3"><StatusBadge status={testCase.visibility} /></td>
                            <td className="py-3 text-white/50">{testCase.input_preview}</td>
                            <td className="py-3 text-white/50">{testCase.expected_output_preview}</td>
                            <td className="py-3 text-cyanGlow">{testCase.points}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}
