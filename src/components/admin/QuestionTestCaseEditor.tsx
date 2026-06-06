"use client";

import { Loader2, Plus, Save, Trash2, Wand2 } from "lucide-react";
import { useState } from "react";
import {
  createQuestion,
  createTestCase,
  deleteQuestion,
  deleteTestCase,
  generateQuestionDraft,
  updateQuestion,
  updateTestCase
} from "@/lib/api";
import { defaultTestCode, normalizeTestCode } from "@/lib/languages";
import type { AdminTestCase, Assessment, AuthoringSource, Difficulty, Language, Question, TaskType, VerificationMode } from "@/lib/types";

interface QuestionTestCaseEditorProps {
  assessment: Assessment;
  onAssessmentChange: (assessment: Assessment) => void;
}

const defaultStarterCode = {
  python: { "solution.py": "def solve():\n    pass\n" },
  javascript: { "solution.js": "function solve() {\n}\n\nmodule.exports = { solve };\n" },
  typescript: { "solution.ts": "function solve(): unknown {\n  return null;\n}\n" }
};

const taskTypes: Array<{ value: TaskType; label: string }> = [
  { value: "frontend_ui_extension", label: "Frontend UI extension" },
  { value: "rest_api_development", label: "REST API development" },
  { value: "database_query_schema", label: "Database query/schema" },
  { value: "bug_fix", label: "Bug fix" },
];

const difficulties: Difficulty[] = ["easy", "medium", "hard"];

const verificationModes: Array<{ value: VerificationMode; label: string }> = [
  { value: "browser_ui_preview", label: "Browser UI preview" },
  { value: "api_response_check", label: "API response check" },
  { value: "database_result_check", label: "Database result check" },
  { value: "automated_test", label: "Automated test" },
  { value: "regression_test", label: "Regression test" }
];

const authoringSources: AuthoringSource[] = ["manual", "llm_generated", "admin_edited"];
const studentLanguages: Language[] = ["python", "javascript"];

interface PendingEditorAction {
  key: string;
  label: string;
}

export function QuestionTestCaseEditor({ assessment, onAssessmentChange }: QuestionTestCaseEditorProps) {
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingEditorAction | null>(null);
  const [draftTaskType, setDraftTaskType] = useState<TaskType>("frontend_ui_extension");
  const [draftDifficulty, setDraftDifficulty] = useState<Difficulty>("medium");
  const [draftLanguages, setDraftLanguages] = useState<Language[]>(["python", "javascript"]);

  async function runEditorAction(action: () => Promise<void>, successMessage: string, nextPendingAction: PendingEditorAction) {
    if (pendingAction !== null) {
      return;
    }

    setStatus(null);
    setError(null);
    setPendingAction(nextPendingAction);

    try {
      await action();
      setStatus(successMessage);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to save changes.");
    } finally {
      setPendingAction(null);
    }
  }

  function isActionPending(key: string) {
    return pendingAction?.key === key;
  }

  function updateQuestionState(questionId: string, update: Partial<Question>) {
    onAssessmentChange({
      ...assessment,
      questions: assessment.questions.map((question) =>
        question.question_id === questionId ? { ...question, ...update } : question
      )
    });
  }

  function updateQuestionMetadata(
    questionId: string,
    metadataField: "verification_metadata" | "grading_configuration" | "traceability_metadata",
    metadataKey: string,
    value: string
  ) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    updateQuestionState(questionId, {
      [metadataField]: {
        ...(question[metadataField] ?? {}),
        [metadataKey]: value
      }
    } as Partial<Question>);
  }

  function updateQuestionLanguage(questionId: string, language: Language, checked: boolean) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    const current = question.language_constraints.filter((item) => item === "python" || item === "javascript");
    const nextLanguages = checked
      ? Array.from(new Set([...current, language]))
      : current.filter((item) => item !== language);

    updateQuestionState(questionId, { language_constraints: nextLanguages.length ? nextLanguages : current });
  }

  function toggleDraftLanguage(language: Language, checked: boolean) {
    setDraftLanguages((current) => {
      const nextLanguages = checked
        ? Array.from(new Set([...current, language]))
        : current.filter((item) => item !== language);

      return nextLanguages.length ? nextLanguages : current;
    });
  }

  function getFirstFileContent(files: Record<string, string> | undefined): string {
    if (!files) return "";
    const values = Object.values(files);
    return values[0] ?? "";
  }

  function updateStarterCode(questionId: string, language: Language, value: string) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    const currentFiles = question.starter_code[language] ?? {};
    const firstFileName = Object.keys(currentFiles)[0] ?? (language === "javascript" ? "solution.js" : language === "typescript" ? "solution.ts" : "solution.py");

    updateQuestionState(questionId, {
      starter_code: {
        ...question.starter_code,
        [language]: { ...currentFiles, [firstFileName]: value }
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

  function updateTestCode(questionId: string, testCaseId: string, language: Language, value: string) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    const testCase = question?.admin_test_cases?.find((item) => item.test_case_id === testCaseId);
    if (!question || !testCase) {
      return;
    }

    updateTestCaseState(questionId, testCaseId, {
      test_code: {
        ...testCase.test_code,
        [language]: value
      }
    });
  }

  async function addQuestion() {
    const sortOrder = assessment.questions.length + 1;
    const question = await createQuestion(assessment.assessment_id, {
      question_id: "new",
      title: `Question ${sortOrder}`,
      task_type: "rest_api_development",
      difficulty: "medium",
      verification_mode: "api_response_check",
      starter_prototype_reference: assessment.shared_prototype_reference ?? null,
      problem_description_markdown: "Describe the task.",
      admin_notes: "",
      sort_order: sortOrder,
      max_score: 100,
      constraints: [],
      language_constraints: ["python", "javascript"],
      starter_code: defaultStarterCode,
      starter_files_metadata: {},
      verification_metadata: { primary_view: "api_response_check" },
      grading_configuration: { runner: "automated_tests", requires_student_install: "false" },
      authoring_source: "manual",
      traceability_metadata: { requirements: "REQ-12,REQ-13,REQ-15" },
      public_examples: [],
      admin_test_cases: []
    });

    onAssessmentChange({
      ...assessment,
      question_count: assessment.question_count + 1,
      questions: [...assessment.questions, question]
    });
  }

  async function addGeneratedDraftQuestion() {
    const question = await generateQuestionDraft(assessment.assessment_id, {
      task_type: draftTaskType,
      difficulty: draftDifficulty,
      supported_languages: draftLanguages,
      starter_prototype_reference: assessment.shared_prototype_reference ?? null
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
      test_code: defaultTestCode,
      authoring_source: "manual",
      public_metadata: { student_visible: "true" },
      admin_metadata: {},
      traceability_metadata: { requirements: "REQ-15,REQ-52,REQ-53" }
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
          <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(addQuestion, "Question added.", { key: "add-question", label: "Creating question in backend..." })}>
            {isActionPending("add-question") ? <Loader2 className="animate-spin" size={15} /> : <Plus size={15} />}
            {isActionPending("add-question") ? "Creating..." : "Add question"}
          </button>
        </div>
        <div className="mt-4 rounded-xl border border-white/10 bg-black/20 p-4">
          <div className="grid gap-3 lg:grid-cols-[1.2fr_0.8fr_1fr_auto]">
            <label className="grid gap-2 text-sm text-white/60">
              Generated draft task type
              <select className="field w-full" value={draftTaskType} onChange={(event) => setDraftTaskType(event.target.value as TaskType)}>
                {taskTypes.map((taskType) => (
                  <option key={taskType.value} value={taskType.value}>{taskType.label}</option>
                ))}
              </select>
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Difficulty
              <select className="field w-full" value={draftDifficulty} onChange={(event) => setDraftDifficulty(event.target.value as Difficulty)}>
                {difficulties.map((difficulty) => (
                  <option key={difficulty} value={difficulty}>{difficulty}</option>
                ))}
              </select>
            </label>
            <div className="grid gap-2 text-sm text-white/60">
              Supported languages
              <div className="flex flex-wrap gap-3 rounded-lg border border-white/10 bg-white/5 px-3 py-2">
                {studentLanguages.map((language) => (
                  <label key={language} className="flex items-center gap-2 text-xs text-white/60">
                    <input type="checkbox" checked={draftLanguages.includes(language)} onChange={(event) => toggleDraftLanguage(language, event.target.checked)} />
                    {language}
                  </label>
                ))}
              </div>
            </div>
            <div className="flex items-end">
              <button
                className="btn-secondary px-3 py-2"
                type="button"
                disabled={pendingAction !== null}
                onClick={() => runEditorAction(addGeneratedDraftQuestion, "Generated task draft added for review.", { key: "generate-draft", label: "Waiting for the configured AI provider to return a real draft..." })}
              >
                {isActionPending("generate-draft") ? <Loader2 className="animate-spin" size={15} /> : <Wand2 size={15} />}
                {isActionPending("generate-draft") ? "Generating..." : "Generate draft"}
              </button>
            </div>
          </div>
          <p className="mt-3 text-xs text-white/40">
            Generated tasks are review drafts. Keep the assessment in draft while editing, then publish by changing the assessment status to active.
          </p>
        </div>
        {pendingAction ? <p className="mt-3 text-sm text-white/55" aria-live="polite">{pendingAction.label}</p> : null}
        {status ? <p className="mt-3 text-sm text-cyanGlow">{status}</p> : null}
        {error ? <p className="mt-3 text-sm text-pinkGlow">{error}</p> : null}
        <div className="mt-4 space-y-4">
          {assessment.questions.map((question) => (
            <article key={question.question_id} className="rounded-2xl border border-white/10 bg-black/20 p-4">
              <div className="grid gap-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question editor</p>
                  <div className="flex flex-wrap gap-2">
                    <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => saveQuestion(question), "Question saved.", { key: `save-question-${question.question_id}`, label: "Saving question changes in backend..." })}>
                      {isActionPending(`save-question-${question.question_id}`) ? <Loader2 className="animate-spin" size={15} /> : <Save size={15} />}
                      {isActionPending(`save-question-${question.question_id}`) ? "Saving..." : "Save question"}
                    </button>
                    <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => removeQuestion(question.question_id), "Question deleted.", { key: `delete-question-${question.question_id}`, label: "Deleting question in backend..." })}>
                      {isActionPending(`delete-question-${question.question_id}`) ? <Loader2 className="animate-spin" size={15} /> : <Trash2 size={15} />}
                      {isActionPending(`delete-question-${question.question_id}`) ? "Deleting..." : "Delete"}
                    </button>
                  </div>
                </div>
                <div className="grid gap-3 grid-cols-1 sm:grid-cols-[2fr_1fr_1fr]">
                  <label className="grid gap-2 text-sm text-white/60">
                    Title
                    <input className="field w-full" value={question.title} onChange={(event) => updateQuestionState(question.question_id, { title: event.target.value })} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Sort order
                    <input className="field w-full" type="number" value={question.sort_order ?? 0} onChange={(event) => updateQuestionState(question.question_id, { sort_order: Number(event.target.value) })} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Max score
                    <input className="field w-full" type="number" value={question.max_score ?? 100} onChange={(event) => updateQuestionState(question.question_id, { max_score: Number(event.target.value) })} />
                  </label>
                </div>
                <div className="grid gap-3 grid-cols-1 sm:grid-cols-2 xl:grid-cols-4">
                  <label className="grid gap-2 text-sm text-white/60">
                    Task type
                    <select className="field w-full" value={question.task_type ?? "rest_api_development"} onChange={(event) => updateQuestionState(question.question_id, { task_type: event.target.value as TaskType })}>
                      {taskTypes.map((taskType) => (
                        <option key={taskType.value} value={taskType.value}>{taskType.label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Difficulty
                    <select className="field w-full" value={question.difficulty ?? "medium"} onChange={(event) => updateQuestionState(question.question_id, { difficulty: event.target.value as Difficulty })}>
                      {difficulties.map((difficulty) => (
                        <option key={difficulty} value={difficulty}>{difficulty}</option>
                      ))}
                    </select>
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Verification mode
                    <select className="field w-full" value={question.verification_mode ?? "automated_test"} onChange={(event) => updateQuestionState(question.question_id, { verification_mode: event.target.value as VerificationMode })}>
                      {verificationModes.map((mode) => (
                        <option key={mode.value} value={mode.value}>{mode.label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Authoring source
                    <select className="field w-full" value={question.authoring_source ?? "manual"} onChange={(event) => updateQuestionState(question.question_id, { authoring_source: event.target.value as AuthoringSource })}>
                      {authoringSources.map((source) => (
                        <option key={source} value={source}>{source}</option>
                      ))}
                    </select>
                  </label>
                </div>
                <div className="grid gap-3 lg:grid-cols-[1fr_1fr_1fr]">
                  <label className="grid gap-2 text-sm text-white/60">
                    Starter prototype reference
                    <input className="field w-full" value={question.starter_prototype_reference ?? ""} onChange={(event) => updateQuestionState(question.question_id, { starter_prototype_reference: event.target.value })} />
                  </label>
                  <div className="grid gap-2 text-sm text-white/60">
                    Supported student languages
                    <div className="flex flex-wrap gap-3 rounded-lg border border-white/10 bg-white/5 px-3 py-2">
                      {studentLanguages.map((language) => (
                        <label key={language} className="flex items-center gap-2 text-xs text-white/60">
                          <input type="checkbox" checked={question.language_constraints.includes(language)} onChange={(event) => updateQuestionLanguage(question.question_id, language, event.target.checked)} />
                          {language}
                        </label>
                      ))}
                    </div>
                  </div>
                  <label className="grid gap-2 text-sm text-white/60">
                    Requirement IDs
                    <input className="field w-full" value={question.traceability_metadata?.requirements ?? ""} onChange={(event) => updateQuestionMetadata(question.question_id, "traceability_metadata", "requirements", event.target.value)} placeholder="REQ-17,REQ-18d" />
                  </label>
                </div>
                <div className="grid gap-3 lg:grid-cols-3">
                  <label className="grid gap-2 text-sm text-white/60">
                    Preview entry / endpoint / result key
                    <input
                      className="field w-full"
                      value={question.verification_metadata?.preview_entry ?? question.verification_metadata?.endpoint ?? question.verification_metadata?.result_shape ?? question.verification_metadata?.focus ?? ""}
                      onChange={(event) => {
                        const metadataKey = question.verification_mode === "browser_ui_preview"
                          ? "preview_entry"
                          : question.verification_mode === "api_response_check"
                            ? "endpoint"
                            : question.verification_mode === "database_result_check"
                              ? "result_shape"
                              : "focus";
                        updateQuestionMetadata(question.question_id, "verification_metadata", metadataKey, event.target.value);
                      }}
                    />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Grading runner
                    <input className="field w-full" value={question.grading_configuration?.runner ?? "automated_tests"} onChange={(event) => updateQuestionMetadata(question.question_id, "grading_configuration", "runner", event.target.value)} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Requires student install
                    <select className="field w-full" value={question.grading_configuration?.requires_student_install ?? "false"} onChange={(event) => updateQuestionMetadata(question.question_id, "grading_configuration", "requires_student_install", event.target.value)}>
                      <option value="false">false</option>
                    </select>
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
                    <textarea className="field min-h-32 font-mono" value={getFirstFileContent(question.starter_code.python)} onChange={(event) => updateStarterCode(question.question_id, "python", event.target.value)} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    JavaScript starter code
                    <textarea className="field min-h-32 font-mono" value={getFirstFileContent(question.starter_code.javascript)} onChange={(event) => updateStarterCode(question.question_id, "javascript", event.target.value)} />
                  </label>
                </div>
              </div>

              <div className="mt-5 border-t border-white/10 pt-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h3 className="text-sm font-semibold text-white/80">Test cases</h3>
                  <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => addTestCase(question.question_id), "Test case added.", { key: `add-test-${question.question_id}`, label: "Creating test case in backend..." })}>
                    {isActionPending(`add-test-${question.question_id}`) ? <Loader2 className="animate-spin" size={15} /> : <Plus size={15} />}
                    {isActionPending(`add-test-${question.question_id}`) ? "Creating..." : "Add test case"}
                  </button>
                </div>
                <div className="mt-3 space-y-3">
                  {(question.admin_test_cases ?? []).map((testCase) => (
                    <div key={testCase.test_case_id} className="rounded-xl border border-white/10 bg-white/5 p-3">
                      {(() => {
                        const testCode = normalizeTestCode(testCase.test_code);
                        return (
                          <>
                            <div className="grid gap-3 grid-cols-1 sm:grid-cols-[2fr_1fr_1fr_auto]">
                              <label className="grid gap-2 text-sm text-white/60">
                                Name
                                <input className="field w-full" value={testCase.name} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { name: event.target.value })} />
                              </label>
                              <label className="grid gap-2 text-sm text-white/60">
                                Visibility
                                <select className="field w-full" value={testCase.visibility} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { visibility: event.target.value as AdminTestCase["visibility"] })}>
                                  <option value="public">public</option>
                                  <option value="hidden">hidden</option>
                                </select>
                              </label>
                              <label className="grid gap-2 text-sm text-white/60">
                                Source
                                <select className="field w-full" value={testCase.authoring_source ?? "manual"} onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, { authoring_source: event.target.value as AuthoringSource })}>
                                  {authoringSources.map((source) => (
                                    <option key={source} value={source}>{source}</option>
                                  ))}
                                </select>
                              </label>
                              <div className="flex items-end gap-2">
                                <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => saveTestCase(testCase), "Test case saved.", { key: `save-test-${testCase.test_case_id}`, label: "Saving test case in backend..." })}>
                                  {isActionPending(`save-test-${testCase.test_case_id}`) ? <Loader2 className="animate-spin" size={15} /> : <Save size={15} />}
                                  {isActionPending(`save-test-${testCase.test_case_id}`) ? "Saving..." : "Save"}
                                </button>
                                <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => removeTestCase(question.question_id, testCase.test_case_id), "Test case deleted.", { key: `delete-test-${testCase.test_case_id}`, label: "Deleting test case in backend..." })}>
                                  {isActionPending(`delete-test-${testCase.test_case_id}`) ? <Loader2 className="animate-spin" size={15} /> : <Trash2 size={15} />}
                                </button>
                              </div>
                            </div>
                            <label className="mt-3 grid gap-2 text-sm text-white/60">
                              Test traceability requirement IDs
                              <input
                                className="field w-full"
                                value={testCase.traceability_metadata?.requirements ?? ""}
                                onChange={(event) => updateTestCaseState(question.question_id, testCase.test_case_id, {
                                  traceability_metadata: {
                                    ...(testCase.traceability_metadata ?? {}),
                                    requirements: event.target.value
                                  }
                                })}
                                placeholder="REQ-15,REQ-52,REQ-53"
                              />
                            </label>
                            <div className="mt-3 grid gap-3">
                              <label className="grid gap-2 text-sm text-white/60">
                                Python pytest code
                                <textarea className="field min-h-40 font-mono" value={testCode.python} onChange={(event) => updateTestCode(question.question_id, testCase.test_case_id, "python", event.target.value)} />
                              </label>
                              <label className="grid gap-2 text-sm text-white/60">
                                JavaScript Jest code
                                <textarea className="field min-h-40 font-mono" value={testCode.javascript} onChange={(event) => updateTestCode(question.question_id, testCase.test_case_id, "javascript", event.target.value)} />
                              </label>
                            </div>
                          </>
                        );
                      })()}
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
