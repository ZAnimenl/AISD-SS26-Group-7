"use client";

import { Plus, Save, Trash2 } from "lucide-react";
import { useState } from "react";
import {
  createQuestion,
  createTestCase,
  deleteQuestion,
  deleteTestCase,
  updateQuestion,
  updateTestCase
} from "@/lib/api";
import type { AdminTestCase, Assessment, Language, Question } from "@/lib/types";

interface QuestionTestCaseEditorProps {
  assessment: Assessment;
  onAssessmentChange: (assessment: Assessment) => void;
}

const defaultStarterCode = {
  python: "def solve():\n    pass\n",
  javascript: "function solve() {\n}\n"
};

export function QuestionTestCaseEditor({ assessment, onAssessmentChange }: QuestionTestCaseEditorProps) {
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function runEditorAction(action: () => Promise<void>, successMessage: string) {
    setStatus(null);
    setError(null);

    try {
      await action();
      setStatus(successMessage);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to save changes.");
    }
  }

  function updateQuestionState(questionId: string, update: Partial<Question>) {
    onAssessmentChange({
      ...assessment,
      questions: assessment.questions.map((question) =>
        question.question_id === questionId ? { ...question, ...update } : question
      )
    });
  }

  function updateStarterCode(questionId: string, language: Language, value: string) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    updateQuestionState(questionId, {
      starter_code: {
        ...question.starter_code,
        [language]: value
      }
    });
  }

  function updateTestCaseState(questionId: string, testCaseId: string, update: Partial<AdminTestCase>) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    updateQuestionState(questionId, {
      admin_test_cases: (question.admin_test_cases ?? []).map((testCase) =>
        testCase.test_case_id === testCaseId ? { ...testCase, ...update } : testCase
      )
    });
  }

  async function addQuestion() {
    const sortOrder = assessment.questions.length + 1;
    const question = await createQuestion(assessment.assessment_id, {
      question_id: "new",
      title: `Question ${sortOrder}`,
      problem_description_markdown: "Describe the task.",
      admin_notes: "",
      sort_order: sortOrder,
      max_score: 100,
      constraints: [],
      language_constraints: ["python", "javascript"],
      starter_code: defaultStarterCode,
      public_examples: [],
      admin_test_cases: []
    });

    onAssessmentChange({
      ...assessment,
      question_count: assessment.question_count + 1,
      questions: [...assessment.questions, question]
    });
  }

  async function saveQuestion(question: Question) {
    const savedQuestion = await updateQuestion(question);
    onAssessmentChange({
      ...assessment,
      questions: assessment.questions.map((item) =>
        item.question_id === savedQuestion.question_id
          ? { ...savedQuestion, admin_test_cases: question.admin_test_cases ?? [] }
          : item
      )
    });
  }

  async function removeQuestion(questionId: string) {
    await deleteQuestion(questionId);
    onAssessmentChange({
      ...assessment,
      question_count: Math.max(0, assessment.question_count - 1),
      questions: assessment.questions.filter((question) => question.question_id !== questionId)
    });
  }

  async function addTestCase(questionId: string) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    const nextTestCase: AdminTestCase = {
      test_case_id: "new",
      name: `test ${(question.admin_test_cases?.length ?? 0) + 1}`,
      visibility: "public",
      input: "",
      expected_output: "",
      input_preview: "",
      expected_output_preview: ""
    };
    const testCaseId = await createTestCase(questionId, nextTestCase);

    updateQuestionState(questionId, {
      admin_test_cases: [
        ...(question.admin_test_cases ?? []),
        {
          ...nextTestCase,
          test_case_id: testCaseId
        }
      ]
    });
  }

  async function saveTestCase(testCase: AdminTestCase) {
    await updateTestCase(testCase);
  }

  async function removeTestCase(questionId: string, testCaseId: string) {
    await deleteTestCase(testCaseId);
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    updateQuestionState(questionId, {
      admin_test_cases: (question.admin_test_cases ?? []).filter((testCase) => testCase.test_case_id !== testCaseId)
    });
  }

  return (
    <section className="panel">
      <div className="relative">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Questions and test cases</h2>
          <button className="btn-secondary px-3 py-2" type="button" onClick={() => runEditorAction(addQuestion, "Question added.")}>
            <Plus size={15} />
            Add question
          </button>
        </div>
        {status ? <p className="mt-3 text-sm text-cyanGlow">{status}</p> : null}
        {error ? <p className="mt-3 text-sm text-pinkGlow">{error}</p> : null}
        <div className="mt-4 space-y-4">
          {assessment.questions.map((question) => (
            <article key={question.question_id} className="rounded-2xl border border-white/10 bg-black/20 p-4">
              <div className="grid gap-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question editor</p>
                  <div className="flex flex-wrap gap-2">
                    <button className="btn-secondary px-3 py-2" type="button" onClick={() => runEditorAction(() => saveQuestion(question), "Question saved.")}>
                      <Save size={15} />
                      Save question
                    </button>
                    <button className="btn-secondary px-3 py-2" type="button" onClick={() => runEditorAction(() => removeQuestion(question.question_id), "Question deleted.")}>
                      <Trash2 size={15} />
                      Delete
                    </button>
                  </div>
                </div>
                <div className="grid gap-3 lg:grid-cols-[1fr_120px_120px]">
                  <label className="grid gap-2 text-sm text-white/60">
                    Title
                    <input className="field" value={question.title} onChange={(event) => updateQuestionState(question.question_id, { title: event.target.value })} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Sort order
                    <input className="field" type="number" value={question.sort_order ?? 0} onChange={(event) => updateQuestionState(question.question_id, { sort_order: Number(event.target.value) })} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Max score
                    <input className="field" type="number" value={question.max_score ?? 100} onChange={(event) => updateQuestionState(question.question_id, { max_score: Number(event.target.value) })} />
                  </label>
                </div>
                <label className="grid gap-2 text-sm text-white/60">
                  Problem description
                  <textarea className="field min-h-28" value={question.problem_description_markdown} onChange={(event) => updateQuestionState(question.question_id, { problem_description_markdown: event.target.value })} />
                </label>
                <label className="grid gap-2 text-sm text-white/60">
                  Admin notes
                  <textarea className="field min-h-20" value={question.admin_notes ?? ""} onChange={(event) => updateQuestionState(question.question_id, { admin_notes: event.target.value })} />
                </label>
                <div className="grid gap-3 lg:grid-cols-2">
                  <label className="grid gap-2 text-sm text-white/60">
                    Python starter code
                    <textarea className="field min-h-32 font-mono" value={question.starter_code.python} onChange={(event) => updateStarterCode(question.question_id, "python", event.target.value)} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    JavaScript starter code
                    <textarea className="field min-h-32 font-mono" value={question.starter_code.javascript} onChange={(event) => updateStarterCode(question.question_id, "javascript", event.target.value)} />
                  </label>
                </div>
              </div>

              <div className="mt-5 border-t border-white/10 pt-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h3 className="text-sm font-semibold text-white/80">Test cases</h3>
                  <button className="btn-secondary px-3 py-2" type="button" onClick={() => runEditorAction(() => addTestCase(question.question_id), "Test case added.")}>
                    <Plus size={15} />
                    Add test case
                  </button>
                </div>
                <div className="mt-3 space-y-3">
                  {(question.admin_test_cases ?? []).map((testCase) => (
                    <div key={testCase.test_case_id} className="rounded-xl border border-white/10 bg-white/5 p-3">
                      <div className="grid gap-3 lg:grid-cols-[1fr_140px_auto]">
                        <label className="grid gap-2 text-sm text-white/60">
                          Name
                          <input className="field" value={testCase.name} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { name: event.target.value })} />
                        </label>
                        <label className="grid gap-2 text-sm text-white/60">
                          Visibility
                          <select className="field" value={testCase.visibility} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { visibility: event.target.value as AdminTestCase["visibility"] })}>
                            <option value="public">public</option>
                            <option value="hidden">hidden</option>
                          </select>
                        </label>
                        <div className="flex items-end gap-2">
                          <button className="btn-secondary px-3 py-2" type="button" onClick={() => runEditorAction(() => saveTestCase(testCase), "Test case saved.")}>
                            <Save size={15} />
                            Save
                          </button>
                          <button className="btn-secondary px-3 py-2" type="button" onClick={() => runEditorAction(() => removeTestCase(question.question_id, testCase.test_case_id), "Test case deleted.")}>
                            <Trash2 size={15} />
                          </button>
                        </div>
                      </div>
                      <div className="mt-3 grid gap-3 lg:grid-cols-2">
                        <label className="grid gap-2 text-sm text-white/60">
                          Input
                          <textarea className="field min-h-24 font-mono" value={testCase.input ?? ""} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { input: event.target.value, input_preview: event.target.value })} />
                        </label>
                        <label className="grid gap-2 text-sm text-white/60">
                          Expected output
                          <textarea className="field min-h-24 font-mono" value={testCase.expected_output ?? ""} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { expected_output: event.target.value, expected_output_preview: event.target.value })} />
                        </label>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
