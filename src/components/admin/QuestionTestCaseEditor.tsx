"use client";

import { Loader2, Minus, Plus, Save, Trash2, Wand2 } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import {
  createQuestion,
  createTestCase,
  deleteQuestion,
  deleteTestCase,
  generateAssessmentBlueprint,
  regenerateQuestionDraft,
  updateQuestion,
  updateTestCase
} from "@/lib/api";
import {
  defaultStarterCode,
  defaultTestCode,
  getDefaultLanguagesForTaskType,
  getLanguageLabel,
  normalizeStudentLanguageConstraints,
  normalizeTestCode,
  STUDENT_LANGUAGE_OPTIONS
} from "@/lib/languages";
import type { AdminTestCase, Assessment, AuthoringSource, Difficulty, Language, Question, TaskType, VerificationMode } from "@/lib/types";
import { CustomDropdown } from "@/components/ui/CustomDropdown";

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
const MAX_TASKS_PER_TYPE = 5;
const MAX_TOTAL_TASKS = 12;

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

type TokenEfficiencyReferenceBaseline = {
  status?: string;
  Status?: string;
  referenceScore?: number;
  ReferenceScore?: number;
  compressionRatio?: number;
  CompressionRatio?: number;
  structuralUtilityRetention?: number;
  StructuralUtilityRetention?: number;
  compactInputTokens?: number;
  CompactInputTokens?: number;
  fullInputTokens?: number;
  FullInputTokens?: number;
  failureReason?: string;
  FailureReason?: string;
  standardSteps?: TokenEfficiencyStandardStep[];
  StandardSteps?: TokenEfficiencyStandardStep[];
};

type TokenEfficiencyStandardStep = {
  purpose?: string;
  Purpose?: string;
  minimalInput?: string;
  MinimalInput?: string;
  publicVerification?: string;
  PublicVerification?: string;
};

function readTokenEfficiencyBaseline(question: Question): TokenEfficiencyReferenceBaseline | null {
  const serializedBenchmark = question.grading_configuration?.ai_usage_benchmark;
  if (!serializedBenchmark) return null;

  try {
    const benchmark = JSON.parse(serializedBenchmark) as Record<string, unknown>;
    const baseline = benchmark.referenceBaseline ?? benchmark.ReferenceBaseline;
    return baseline && typeof baseline === "object" ? baseline as TokenEfficiencyReferenceBaseline : null;
  } catch {
    return null;
  }
}

function baselineValue(baseline: TokenEfficiencyReferenceBaseline, camel: keyof TokenEfficiencyReferenceBaseline, pascal: keyof TokenEfficiencyReferenceBaseline) {
  return baseline[camel] ?? baseline[pascal];
}

function TokenEfficiencyBaselineCard({ question }: { question: Question }) {
  const baseline = readTokenEfficiencyBaseline(question);
  if (!baseline) return null;

  const status = baseline.status ?? baseline.Status ?? "unavailable";
  if (status !== "complete") {
    const failureReason = baseline.failureReason ?? baseline.FailureReason;
    if (failureReason === "baseline_response_missing_required_context") {
      return null;
    }

    return (
      <div className="rounded-xl border border-amber-300/25 bg-amber-300/5 p-3 text-sm text-amber-100/80">
        <p className="font-medium">Reference token baseline unavailable</p>
        <p className="mt-1 text-xs text-white/45">The generated task remains a draft. Generate again when a provider is available to record its reference baseline.</p>
      </div>
    );
  }

  const score = Number(baselineValue(baseline, "referenceScore", "ReferenceScore") ?? 0);
  const ratio = Number(baselineValue(baseline, "compressionRatio", "CompressionRatio") ?? 0);
  const retention = Number(baselineValue(baseline, "structuralUtilityRetention", "StructuralUtilityRetention") ?? 0);
  const compactTokens = Number(baselineValue(baseline, "compactInputTokens", "CompactInputTokens") ?? 0);
  const fullTokens = Number(baselineValue(baseline, "fullInputTokens", "FullInputTokens") ?? 0);
  const standardSteps = baseline.standardSteps ?? baseline.StandardSteps ?? [];
  return (
    <div className="rounded-xl border border-cyanGlow/25 bg-cyanGlow/5 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm font-medium text-cyanGlow">Provider-measured reference baseline</p>
        <span className="font-mono text-sm text-white/85">{score}/100</span>
      </div>
      <div className="mt-3 grid gap-2 text-xs text-white/55 sm:grid-cols-3">
        <p>Compression ratio <span className="font-mono text-white/80">{ratio.toFixed(2)}×</span></p>
        <p>Structural retention <span className="font-mono text-white/80">{Math.round(retention * 100)}%</span></p>
        <p>Input tokens <span className="font-mono text-white/80">{compactTokens} / {fullTokens}</span></p>
      </div>
      {standardSteps.length > 0 ? (
        <div className="mt-3 border-t border-white/10 pt-3">
          <p className="text-xs font-medium text-white/75">Minimal-input standard steps</p>
          <ol className="mt-2 grid gap-2 text-xs text-white/50">
            {standardSteps.map((step, index) => {
              const purpose = step.purpose ?? step.Purpose ?? "Reference step";
              const minimalInput = step.minimalInput ?? step.MinimalInput ?? "";
              const verification = step.publicVerification ?? step.PublicVerification ?? "";
              return (
                <li key={`${purpose}-${index}`} className="rounded-lg bg-black/20 p-2">
                  <p className="font-medium text-white/75">{index + 1}. {purpose}</p>
                  <p className="mt-1"><span className="text-white/35">Minimal input:</span> {minimalInput}</p>
                  <p className="mt-1"><span className="text-white/35">Public verification:</span> {verification}</p>
                </li>
              );
            })}
          </ol>
        </div>
      ) : null}
      <p className="mt-2 text-[11px] leading-4 text-white/35">Administrator-only reference metadata; it is not a student grade.</p>
    </div>
  );
}

export function QuestionTestCaseEditor({ assessment, onAssessmentChange }: QuestionTestCaseEditorProps) {
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingEditorAction | null>(null);
  const [selectedQuestionId, setSelectedQuestionId] = useState<string | null>(
    assessment.questions[0]?.question_id ?? null
  );
  const [blueprintDifficulty, setBlueprintDifficulty] = useState<Difficulty>("medium");
  const [blueprintTaskCounts, setBlueprintTaskCounts] = useState<Record<TaskType, number>>({
    frontend_ui_extension: 1,
    rest_api_development: 1,
    database_query_schema: 1,
    bug_fix: 1
  });
  const orderedQuestions = useMemo(
    () => [...assessment.questions].sort((left, right) =>
      (left.sort_order ?? 0) - (right.sort_order ?? 0)
      || left.question_id.localeCompare(right.question_id)
    ),
    [assessment.questions]
  );
  const selectedQuestion = orderedQuestions.find((question) => question.question_id === selectedQuestionId)
    ?? orderedQuestions[0]
    ?? null;
  const blueprintTaskTotal = Object.values(blueprintTaskCounts).reduce((sum, count) => sum + count, 0);

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
      success: generated
        ? "Question and test cases regenerated for review."
        : "Question and test cases generated for review.",
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

  function updateBlueprintTaskCount(taskType: TaskType, requestedCount: number) {
    setBlueprintTaskCounts((current) => {
      const currentTotal = Object.values(current).reduce((sum, count) => sum + count, 0);
      const maximumForType = Math.min(
        MAX_TASKS_PER_TYPE,
        current[taskType] + (MAX_TOTAL_TASKS - currentTotal)
      );

      return {
        ...current,
        [taskType]: Math.max(0, Math.min(maximumForType, requestedCount))
      };
    });
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

  function updateStarterFile(questionId: string, language: Language, fileName: string, value: string) {
    const question = assessment.questions.find((item) => item.question_id === questionId);
    if (!question) {
      return;
    }

    const currentFiles = Object.keys(question.starter_code[language] ?? {}).length
      ? question.starter_code[language]
      : defaultStarterCode[language];

    updateQuestionState(questionId, {
      starter_code: {
        ...question.starter_code,
        [language]: { ...currentFiles, [fileName]: value }
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
    setSelectedQuestionId(question.question_id);
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
    const removedIndex = orderedQuestions.findIndex((question) => question.question_id === questionId);
    const remainingQuestions = orderedQuestions.filter((question) => question.question_id !== questionId);
    const nextQuestion = remainingQuestions[Math.min(Math.max(removedIndex, 0), remainingQuestions.length - 1)];

    onAssessmentChange({
      ...assessment,
      question_count: Math.max(0, assessment.question_count - 1),
      questions: assessment.questions.filter((question) => question.question_id !== questionId)
    });
    setSelectedQuestionId(nextQuestion?.question_id ?? null);
  }

  async function generateBlueprint() {
    const questions = await generateAssessmentBlueprint(assessment.assessment_id, {
      task_type_counts: blueprintTaskCounts,
      difficulty: blueprintDifficulty
    });

    onAssessmentChange({
      ...assessment,
      question_count: questions.length,
      questions
    });
    setSelectedQuestionId(questions[0]?.question_id ?? null);
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
    <section id="questions" className="panel scroll-mt-6">
      <div className="relative">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 flex-wrap items-center gap-3">
            <h2 className="text-lg font-semibold">Questions and test cases</h2>
            {orderedQuestions.length > 0 ? (
              <div className="flex max-w-full flex-wrap gap-2" role="tablist" aria-label="Assessment tasks">
                {orderedQuestions.map((question, index) => {
                  const isSelected = question.question_id === selectedQuestion?.question_id;
                  return (
                    <button
                      key={question.question_id}
                      className={`rounded-lg border px-3 py-1.5 text-xs font-medium transition ${
                        isSelected
                          ? "border-cyanGlow/60 bg-cyanGlow/15 text-cyanGlow"
                          : "border-white/10 bg-white/5 text-white/55 hover:border-white/25 hover:text-white/80"
                      }`}
                      type="button"
                      role="tab"
                      aria-selected={isSelected}
                      aria-controls={`question-editor-${question.question_id}`}
                      title={question.title}
                      disabled={pendingAction !== null}
                      onClick={() => {
                        setSelectedQuestionId(question.question_id);
                        setStatus(null);
                        setError(null);
                      }}
                    >
                      Task {index + 1}
                    </button>
                  );
                })}
              </div>
            ) : null}
          </div>
          <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(addQuestion, "Question added.", { key: "add-question", label: "Creating question..." })}>
            {isActionPending("add-question") ? <Loader2 className="animate-spin" size={15} /> : <Plus size={15} />}
            {isActionPending("add-question") ? "Creating..." : "Add question"}
          </button>
        </div>
        {pendingAction ? <p className="mt-3 text-sm text-white/55" aria-live="polite">{pendingAction.label}</p> : null}
        {status ? <p className="mt-3 text-sm text-cyanGlow">{status}</p> : null}
        {error ? <p className="mt-3 text-sm text-pinkGlow">{error}</p> : null}
        <div className="mt-4 space-y-4">
          {selectedQuestion ? (
            <article
              id={`question-editor-${selectedQuestion.question_id}`}
              role="tabpanel"
              aria-label={`Task ${orderedQuestions.findIndex((question) => question.question_id === selectedQuestion.question_id) + 1}`}
              className="rounded-2xl border border-white/10 bg-black/20 p-4"
            >
              {(() => {
                const question = selectedQuestion;
                return (
                  <>
              <div className="grid gap-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question editor</p>
                  <div className="flex flex-wrap gap-2">
                    <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => saveQuestion(question), "Question saved.", { key: `save-question-${question.question_id}`, label: "Saving question changes..." })}>
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
                    <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => removeQuestion(question.question_id), "Question deleted.", { key: `delete-question-${question.question_id}`, label: "Deleting question..." })}>
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
                <div className="grid gap-3 grid-cols-1 sm:grid-cols-2">
                  <label className="grid gap-2 text-sm text-white/60">
                    Task type
                    <CustomDropdown ariaLabel="Task type" value={question.task_type ?? "rest_api_development"} options={taskTypes} onChange={(value) => updateQuestionTaskType(question.question_id, value)} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Difficulty
                    <CustomDropdown ariaLabel="Difficulty" value={question.difficulty ?? "medium"} options={difficulties.map((value) => ({ value, label: value }))} onChange={(value) => updateQuestionState(question.question_id, { difficulty: value })} />
                  </label>
                </div>
                <label className="grid gap-2 text-sm text-white/60">
                  Problem description
                  <textarea className="field min-h-32" value={question.problem_description_markdown} onChange={(event) => updateQuestionState(question.question_id, { problem_description_markdown: event.target.value })} />
                </label>
                <div className="grid gap-3 grid-cols-1 sm:grid-cols-2">
                  <label className="grid gap-2 text-sm text-white/60">
                    Verification mode
                    <CustomDropdown ariaLabel="Verification mode" value={question.verification_mode ?? "automated_test"} options={verificationModes} onChange={(value) => updateQuestionState(question.question_id, { verification_mode: value })} />
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    Authoring source
                    <CustomDropdown ariaLabel="Authoring source" value={question.authoring_source ?? "manual"} options={authoringSources.map((value) => ({ value, label: value }))} onChange={(value) => updateQuestionState(question.question_id, { authoring_source: value })} />
                  </label>
                </div>
                <TokenEfficiencyBaselineCard question={question} />
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
                  {normalizeStudentLanguageConstraints(question.language_constraints, question.task_type).flatMap((language) => {
                    const files = Object.keys(question.starter_code[language] ?? {}).length
                      ? question.starter_code[language]
                      : defaultStarterCode[language];

                    return Object.entries(files).map(([fileName, content]) => (
                      <label key={`${language}-${fileName}`} className="grid gap-2 text-sm text-white/60">
                        {getLanguageLabel(language)} starter file: {fileName}
                        <textarea className="field min-h-32 font-mono" value={content} onChange={(event) => updateStarterFile(question.question_id, language, fileName, event.target.value)} />
                      </label>
                    ));
                  })}
                </div>
              </div>

              <div className="mt-5 border-t border-white/10 pt-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h3 className="text-sm font-semibold text-white/80">Test cases</h3>
                  <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => addTestCase(question.question_id), "Test case added.", { key: `add-test-${question.question_id}`, label: "Creating test case..." })}>
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
                                <CustomDropdown ariaLabel="Visibility" value={testCase.visibility} options={[{ value: "public", label: "public" }, { value: "hidden", label: "hidden" }]} onChange={(value) => updateTestCaseState(question.question_id, testCase.test_case_id, { visibility: value })} />
                              </label>
                              <label className="grid gap-2 text-sm text-white/60">
                                Source
                                <CustomDropdown ariaLabel="Test source" value={testCase.authoring_source ?? "manual"} options={authoringSources.map((value) => ({ value, label: value }))} onChange={(value) => updateTestCaseState(question.question_id, testCase.test_case_id, { authoring_source: value })} />
                              </label>
                              <div className="flex items-end gap-2">
                                <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => saveTestCase(testCase), "Test case saved.", { key: `save-test-${testCase.test_case_id}`, label: "Saving test case..." })}>
                                  {isActionPending(`save-test-${testCase.test_case_id}`) ? <Loader2 className="animate-spin" size={15} /> : <Save size={15} />}
                                  {isActionPending(`save-test-${testCase.test_case_id}`) ? "Saving..." : "Save"}
                                </button>
                                <button className="btn-secondary px-3 py-2" type="button" disabled={pendingAction !== null} onClick={() => runEditorAction(() => removeTestCase(question.question_id, testCase.test_case_id), "Test case deleted.", { key: `delete-test-${testCase.test_case_id}`, label: "Deleting test case..." })}>
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
                  </>
                );
              })()}
            </article>
          ) : (
            <div className="rounded-2xl border border-cyanGlow/20 bg-black/20 p-5">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question blueprint</p>
                  <h3 className="mt-1 text-lg font-semibold">Choose the assessment mix</h3>
                  <p className="mt-1 max-w-2xl text-sm text-white/45">
                    No questions are configured yet. Generate the complete task set here, or use Add question for manual authoring.
                  </p>
                </div>
                <div className="grid min-w-[124px] place-items-center rounded-xl border border-cyanGlow/25 bg-cyanGlow/10 px-4 py-2 text-center">
                  <p className="text-[10px] uppercase tracking-[0.14em] text-white/40">Total tasks</p>
                  <p className="text-2xl font-semibold text-cyanGlow">{blueprintTaskTotal}</p>
                </div>
              </div>

              <div className="mt-5 grid gap-3 lg:grid-cols-2">
                {taskTypes.map((taskType) => {
                  const count = blueprintTaskCounts[taskType.value];
                  const canIncrease = blueprintTaskTotal < MAX_TOTAL_TASKS && count < MAX_TASKS_PER_TYPE;

                  return (
                    <div key={taskType.value} className="rounded-xl border border-white/10 bg-white/[0.035] p-4">
                      <div className="flex items-center justify-between gap-3">
                        <p className="font-medium text-white/85">{taskType.label}</p>
                        <span className="grid h-9 min-w-9 place-items-center rounded-lg border border-cyanGlow/25 bg-cyanGlow/10 px-2 font-mono text-sm text-cyanGlow">
                          {count}
                        </span>
                      </div>
                      <div className="mt-4 grid grid-cols-[36px_1fr_36px] items-center gap-3">
                        <button
                          className="grid h-9 w-9 place-items-center rounded-full border border-white/15 bg-black/20 text-white/65 transition hover:border-cyanGlow/45 hover:text-cyanGlow disabled:opacity-30"
                          type="button"
                          aria-label={`Remove one ${taskType.label} task`}
                          disabled={pendingAction !== null || count === 0}
                          onClick={() => updateBlueprintTaskCount(taskType.value, count - 1)}
                        >
                          <Minus size={15} />
                        </button>
                        <input
                          className="h-2 w-full cursor-pointer accent-cyan-400"
                          type="range"
                          min={0}
                          max={MAX_TASKS_PER_TYPE}
                          value={count}
                          aria-label={`${taskType.label} question count`}
                          disabled={pendingAction !== null}
                          onChange={(event) => updateBlueprintTaskCount(taskType.value, Number(event.target.value))}
                        />
                        <button
                          className="grid h-9 w-9 place-items-center rounded-full border border-white/15 bg-black/20 text-white/65 transition hover:border-cyanGlow/45 hover:text-cyanGlow disabled:opacity-30"
                          type="button"
                          aria-label={`Add one ${taskType.label} task`}
                          disabled={pendingAction !== null || !canIncrease}
                          onClick={() => updateBlueprintTaskCount(taskType.value, count + 1)}
                        >
                          <Plus size={15} />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="mt-4 flex flex-wrap items-end justify-between gap-4 rounded-xl border border-white/10 bg-black/20 p-4">
                <label className="grid min-w-44 gap-2 text-sm text-white/60">
                  Shared difficulty
                  <CustomDropdown ariaLabel="Shared difficulty" value={blueprintDifficulty} disabled={pendingAction !== null} options={difficulties.map((value) => ({ value, label: value }))} onChange={setBlueprintDifficulty} />
                </label>
                <button
                  className="btn-primary"
                  type="button"
                  disabled={pendingAction !== null || blueprintTaskTotal === 0}
                  onClick={() => runEditorAction(
                    generateBlueprint,
                    `${blueprintTaskTotal} question${blueprintTaskTotal === 1 ? "" : "s"} and their test cases generated for review.`,
                    { key: "generate-blueprint", label: `Generating ${blueprintTaskTotal} validated questions...` }
                  )}
                >
                  {isActionPending("generate-blueprint") ? <Loader2 className="animate-spin" size={16} /> : <Wand2 size={16} />}
                  {isActionPending("generate-blueprint") ? "Generating questions..." : "Generate questions"}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
