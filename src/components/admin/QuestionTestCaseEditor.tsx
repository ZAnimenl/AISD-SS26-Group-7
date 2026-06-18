"use client";

import { Loader2, Plus, Save, Trash2, Wand2 } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  createQuestion,
  createTestCase,
  deleteQuestion,
  deleteTestCase,
  regenerateQuestionDraft,
  updateQuestion,
  updateTestCase
} from "@/lib/api";
import {
  defaultStarterCode,
  defaultTestCode,
  getDefaultFileNameForLanguage,
  getDefaultLanguagesForTaskType,
  getLanguageLabel,
  normalizeStudentLanguageConstraints,
  normalizeTestCode,
  STUDENT_LANGUAGE_OPTIONS
} from "@/lib/languages";
import type { AdminTestCase, Assessment, AuthoringSource, Difficulty, Language, Question, TaskType, VerificationMode } from "@/lib/types";

interface QuestionTestCaseEditorProps {
  assessment: Assessment;
  onAssessmentChange: (assessment: Assessment) => void;
}

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
const studentLanguages = STUDENT_LANGUAGE_OPTIONS;

interface PendingEditorAction {
  key: string;
  label: string;
}

export function QuestionTestCaseEditor({ assessment, onAssessmentChange }: QuestionTestCaseEditorProps) {
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingEditorAction | null>(null);
  const hasSeededFirstQuestionRef = useRef(false);

  const runEditorAction = useCallback(async (action: () => Promise<void>, successMessage: string, nextPendingAction: PendingEditorAction) => {
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
  }, [pendingAction]);

  function isActionPending(key: string) {
    return pendingAction?.key === key;
  }

  function isGeneratedQuestion(question: Question) {
    return question.authoring_source === "llm_generated";
  }

  function getGenerationCopy(question: Question) {
    const generated = isGeneratedQuestion(question);
    return {
      action: generated ? "Regenerate" : "Generate",
      pending: generated ? "Regenerating..." : "Generating...",
      success: generated ? "Question regenerated for review." : "Question generated for review.",
      title: generated
        ? "Regenerate this task from the current problem description"
        : "Generate this task from the current problem description",
      pendingLabel: generated
        ? "Regenerating this task from the current problem description..."
        : "Generating this task from the current problem description..."
    };
  }

  function updateQuestionState(questionId: string, update: Partial<Question>) {
    onAssessmentChange({
      ...assessment,
      questions: assessment.questions.map((question) =>
        question.question_id === questionId ? { ...question, ...update } : question
      )
    });
  }

  function updateQuestionSortOrder(questionId: string, value: number) {
    const maximumSortOrder = Math.max(1, assessment.questions.length);
    const nextSortOrder = Math.min(maximumSortOrder, Math.max(1, value || 1));
    updateQuestionState(questionId, { sort_order: nextSortOrder });
  }

  function updateQuestionLanguage(questionId: string, language: Language, checked: boolean) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    const current = normalizeStudentLanguageConstraints(question.language_constraints, question.task_type);
    const nextLanguages = checked
      ? Array.from(new Set([...current, language]))
      : current.filter((item) => item !== language);

    updateQuestionState(questionId, { language_constraints: nextLanguages.length ? nextLanguages : current });
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
    const firstFileName = Object.keys(currentFiles)[0] ?? getDefaultFileNameForLanguage(language);

    const starterFiles = Object.keys(currentFiles).length ? currentFiles : defaultStarterCode[language];
    const starterFileName = Object.keys(starterFiles)[0] ?? firstFileName;

    updateQuestionState(questionId, {
      starter_code: {
        ...question.starter_code,
        [language]: { ...starterFiles, [starterFileName]: value }
      }
    });
  }

  function updateQuestionTaskType(questionId: string, taskType: TaskType) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    updateQuestionState(questionId, {
      task_type: taskType,
      language_constraints: getDefaultLanguagesForTaskType(taskType)
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

  const addQuestion = useCallback(async () => {
    const sortOrder = assessment.questions.length + 1;
    const question = await createQuestion(assessment.assessment_id, {
      question_id: "new",
      title: `Question ${sortOrder}`,
      task_type: "rest_api_development",
      difficulty: "medium",
      verification_mode: "api_response_check",
      starter_prototype_reference: null,
      problem_description_markdown: "Describe the task.",
      admin_notes: "",
      sort_order: sortOrder,
      max_score: 100,
      constraints: [],
      language_constraints: getDefaultLanguagesForTaskType("rest_api_development"),
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
  }, [assessment, onAssessmentChange]);

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

  async function regenerateQuestion(question: Question) {
    const regeneratedQuestion = await regenerateQuestionDraft(question.question_id, {
      task_type: question.task_type ?? "rest_api_development",
      difficulty: question.difficulty ?? "medium",
      supported_languages: normalizeStudentLanguageConstraints(question.language_constraints, question.task_type),
      starter_prototype_reference: null,
      problem_description_markdown: question.problem_description_markdown
    });

    onAssessmentChange({
      ...assessment,
      questions: assessment.questions.map((item) =>
        item.question_id === question.question_id
          ? {
              ...regeneratedQuestion,
              question_id: question.question_id,
              sort_order: question.sort_order,
              max_score: regeneratedQuestion.max_score ?? question.max_score
            }
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

  useEffect(() => {
    const firstQuestionSeedKey = `admin-assessment-${assessment.assessment_id}-seeded-first-question`;

    if (assessment.questions.length > 0) {
      hasSeededFirstQuestionRef.current = false;
      if (typeof window !== "undefined") {
        window.sessionStorage.removeItem(firstQuestionSeedKey);
      }
      return;
    }

    if (hasSeededFirstQuestionRef.current || pendingAction !== null) {
      return;
    }

    if (typeof window !== "undefined" && window.sessionStorage.getItem(firstQuestionSeedKey) === "true") {
      hasSeededFirstQuestionRef.current = true;
      return;
    }

    if (typeof window !== "undefined") {
      window.sessionStorage.setItem(firstQuestionSeedKey, "true");
    }

    hasSeededFirstQuestionRef.current = true;
    const seedTimer = window.setTimeout(() => {
      void runEditorAction(addQuestion, "Question 1 added.", {
        key: "seed-question-1",
        label: "Creating question 1 in backend..."
      }).finally(() => {
        if (typeof window !== "undefined") {
          window.sessionStorage.removeItem(firstQuestionSeedKey);
        }
      });
    }, 0);

    return () => window.clearTimeout(seedTimer);
  }, [addQuestion, assessment.assessment_id, assessment.questions.length, pendingAction, runEditorAction]);

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
    <section id="questions" className="panel scroll-mt-6">
      <div className="relative">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Questions and test cases</h2>
          <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(addQuestion, "Question added.", { key: "add-question", label: "Creating question in backend..." })}>
            {isActionPending("add-question") ? <Loader2 className="animate-spin" size={15} /> : <Plus size={15} />}
            {isActionPending("add-question") ? "Creating..." : "Add question"}
          </button>
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
                    {(() => {
                      const generationCopy = getGenerationCopy(question);
                      const actionKey = `regenerate-question-${question.question_id}`;
                      return (
                        <button
                          className="btn-secondary px-3 py-2"
                          type="button"
                          title={generationCopy.title}
                          aria-label={generationCopy.title}
                          disabled={pendingAction !== null}
                          onClick={() => runEditorAction(() => regenerateQuestion(question), generationCopy.success, { key: actionKey, label: generationCopy.pendingLabel })}
                        >
                          {isActionPending(actionKey) ? <Loader2 className="animate-spin" size={15} /> : <Wand2 size={15} />}
                          {isActionPending(actionKey) ? generationCopy.pending : generationCopy.action}
                        </button>
                      );
                    })()}
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
                    <input
                      className="field w-full"
                      type="number"
                      min={1}
                      max={Math.max(1, assessment.questions.length)}
                      value={Math.min(Math.max(1, assessment.questions.length), Math.max(1, question.sort_order ?? 1))}
                      onChange={(event) => updateQuestionSortOrder(question.question_id, Number(event.target.value))}
                    />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Max score
                    <input className="field w-full" type="number" value={question.max_score ?? 100} onChange={(event) => updateQuestionState(question.question_id, { max_score: Number(event.target.value) })} />
                  </label>
                </div>
                <label className="grid gap-2 text-sm text-white/60">
                  Problem description
                  <textarea className="field min-h-32" value={question.problem_description_markdown} onChange={(event) => updateQuestionState(question.question_id, { problem_description_markdown: event.target.value })} />
                </label>
                <div className="grid gap-3 grid-cols-1 sm:grid-cols-2 xl:grid-cols-4">
                  <label className="grid gap-2 text-sm text-white/60">
                    Task type
                    <select className="field w-full" value={question.task_type ?? "rest_api_development"} onChange={(event) => updateQuestionTaskType(question.question_id, event.target.value as TaskType)}>
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
                <div className="grid gap-3 lg:grid-cols-1">
                  <div className="grid gap-2 text-sm text-white/60">
                    Supported student languages
                    <div className="flex flex-wrap gap-3 rounded-lg border border-white/10 bg-white/5 px-3 py-2">
                      {studentLanguages.map((language) => (
                        <label key={language.value} className="flex items-center gap-2 text-xs text-white/60">
                          <input type="checkbox" checked={question.language_constraints.includes(language.value)} onChange={(event) => updateQuestionLanguage(question.question_id, language.value, event.target.checked)} />
                          {language.label}
                        </label>
                      ))}
                    </div>
                  </div>
                </div>
                <label className="grid gap-2 text-sm text-white/60">
                  Admin notes
                  <textarea className="field min-h-20" value={question.admin_notes ?? ""} onChange={(event) => updateQuestionState(question.question_id, { admin_notes: event.target.value })} />
                </label>
                <div className="grid gap-3 lg:grid-cols-2">
                  {normalizeStudentLanguageConstraints(question.language_constraints, question.task_type).map((language) => (
                    <label key={language} className="grid gap-2 text-sm text-white/60">
                      {getLanguageLabel(language)} starter code
                      <textarea className="field min-h-32 font-mono" value={getFirstFileContent(question.starter_code[language])} onChange={(event) => updateStarterCode(question.question_id, language, event.target.value)} />
                    </label>
                  ))}
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
                            <div className="mt-3 grid gap-3">
                              {normalizeStudentLanguageConstraints(question.language_constraints, question.task_type).map((language) => (
                                <label key={language} className="grid gap-2 text-sm text-white/60">
                                  {getLanguageLabel(language)} test code
                                  <textarea className="field min-h-40 font-mono" value={testCode[language]} onChange={(event) => updateTestCode(question.question_id, testCase.test_case_id, language, event.target.value)} />
                                </label>
                              ))}
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
