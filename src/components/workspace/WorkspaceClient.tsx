"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { finalizeSubmission, getAiResponse, runCode, saveWorkspace } from "@/lib/api";
import { MonacoCodeEditor } from "@/components/workspace/MonacoCodeEditor";
import { TaskVerificationPreview } from "@/components/workspace/previews/TaskVerificationPreview";
import { ConsolePanel, formatExecutionStatus, getDisplayStatus, getStatusClass } from "@/components/workspace/ConsolePanel";
import { renderMarkdown } from "@/components/workspace/workspaceMarkdown";
import { useWorkspaceIdeLayout } from "@/components/workspace/useWorkspaceIdeLayout";
import { SemanticIcon, type SemanticIconName } from "@/components/ui/SemanticIcon";
import type { AiInteractionType, Assessment, Language, Question, RunResult, TaskType, VerificationMode, WorkspaceQuestionState, WorkspaceState } from "@/lib/types";

interface WorkspaceClientProps {
  assessment: Assessment;
  workspace: WorkspaceState;
}

type SaveState = "saved" | "unsaved" | "saving";

type AiChatMessage = {
  clientId?: string;
  role: "assistant" | "student";
  text: string;
  status?: "pending" | "failed";
  suggestedCode?: string;
  suggestedLanguage?: Language;
  targetFile?: string;
  applyLabel?: string;
  tokenUsage?: {
    input_tokens: number;
    output_tokens: number;
    total_tokens: number;
  };
};

const TASK_LABELS: Record<TaskType, string> = {
  frontend_ui_extension: "Frontend UI extension",
  rest_api_development: "REST API development",
  database_query_schema: "Database query/schema",
  bug_fix: "Bug fix"
};

const VERIFICATION_LABELS: Record<VerificationMode, string> = {
  browser_ui_preview: "Browser UI preview",
  api_response_check: "API response check",
  database_result_check: "Database result check",
  automated_test: "Automated test",
  regression_test: "Regression test"
};

const TASK_ICONS: Record<TaskType, SemanticIconName> = {
  frontend_ui_extension: "frontend",
  rest_api_development: "api",
  database_query_schema: "database",
  bug_fix: "bug"
};

const AI_ACTION_ICONS: Record<AiInteractionType, SemanticIconName> = {
  code_suggestion: "suggestion",
  explanation: "explanation",
  debugging: "debugging"
};

const STUDENT_LANGUAGES: Array<{ value: Language; label: string }> = [
  { value: "python", label: "Python" },
  { value: "javascript", label: "JavaScript" }
];

function getStarterFiles(question: Question | undefined, language: Language): Record<string, string> {
  return question?.starter_code[language] ?? {};
}

function getFileNames(question: Question | undefined, language: Language): string[] {
  const files = getStarterFiles(question, language);
  const names = Object.keys(files);
  return names.length > 0 ? names : [language === "javascript" ? "main.js" : "main.py"];
}

function createQuestionState(question: Question | undefined, language: Language = "python"): WorkspaceQuestionState {
  const starterFiles = getStarterFiles(question, language);
  const fileNames = Object.keys(starterFiles);
  const firstFile = fileNames[0] ?? (language === "javascript" ? "main.js" : "main.py");

  const files: Record<string, { language: Language; content: string }> = {};
  if (fileNames.length > 0) {
    for (const [name, content] of Object.entries(starterFiles)) {
      files[name] = { language, content };
    }
  } else {
    files[firstFile] = { language, content: "" };
  }

  return {
    selected_language: language,
    active_file: firstFile,
    files,
    last_saved_at: "",
    version: 0
  };
}

function getCodeFromState(state: WorkspaceQuestionState | undefined, question: Question | undefined, language: Language, fileName?: string) {
  const targetFile = fileName ?? state?.active_file ?? getFileNames(question, language)[0];
  if (state?.files[targetFile]) {
    return state.files[targetFile].content;
  }
  const starterFiles = getStarterFiles(question, language);
  return starterFiles[targetFile] ?? "";
}

function mergeQuestionStates(current: WorkspaceState["questions"], saved: WorkspaceState["questions"]) {
  return Object.entries(saved).reduce<WorkspaceState["questions"]>((nextStates, [questionId, savedState]) => ({
    ...nextStates,
    [questionId]: {
      ...(nextStates[questionId] ?? savedState),
      ...savedState,
      files: {
        ...(nextStates[questionId]?.files ?? {}),
        ...savedState.files
      }
    }
  }), current);
}

function formatTaskType(taskType?: TaskType) {
  return taskType ? TASK_LABELS[taskType] : "Practical task";
}

function formatVerificationMode(mode?: VerificationMode) {
  return mode ? VERIFICATION_LABELS[mode] : "Automated check";
}

function formatDifficulty(difficulty?: string) {
  return difficulty ? difficulty.charAt(0).toUpperCase() + difficulty.slice(1) : "Unspecified";
}

function formatRemainingTime(expiresAt: string, now: number) {
  const expiry = new Date(expiresAt).getTime();
  if (!Number.isFinite(expiry)) {
    return "Timer unavailable";
  }

  const remainingMs = Math.max(0, expiry - now);
  const totalSeconds = Math.floor(remainingMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  if (totalSeconds <= 0) {
    return "Expired";
  }

  return [hours, minutes, seconds].map((value) => String(value).padStart(2, "0")).join(":");
}

function useRemainingTime(expiresAt: string) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  return formatRemainingTime(expiresAt, now);
}

function getVisibleLanguages(question: Question | undefined) {
  const constraints = question?.language_constraints?.filter((item): item is Language => item === "python" || item === "javascript") ?? [];
  return constraints.length ? constraints : STUDENT_LANGUAGES.map((item) => item.value);
}

export function WorkspaceClient({ assessment, workspace }: WorkspaceClientProps) {
  const firstQuestion = assessment.questions[0];
  if (!firstQuestion) {
    return <EmptyTaskWorkspace />;
  }

  return <WorkspaceWithTasks assessment={assessment} workspace={workspace} firstQuestion={firstQuestion} />;
}

function EmptyTaskWorkspace() {
  return (
    <div className="grid h-[calc(100vh-24px)] place-items-center p-6">
      <section className="panel dynamic-surface max-w-2xl rounded-xl p-6">
        <p className="text-xs uppercase tracking-[0.16em] text-cyanGlow/70">Workspace unavailable</p>
        <h1 className="mt-3 text-2xl font-semibold text-white">No assessment tasks were returned</h1>
        <p className="mt-3 text-sm leading-6 text-white/60">
          This assessment does not have any published tasks yet. Add tasks manually, or create a generated four-task
          draft from the administrator assessment screen.
        </p>
        <div className="mt-5 grid gap-2 text-sm text-white/55 sm:grid-cols-2">
          {Object.values(TASK_LABELS).map((label) => (
            <div key={label} className="rounded-lg border border-white/10 bg-white/5 px-3 py-2">{label}</div>
          ))}
        </div>
      </section>
    </div>
  );
}

function WorkspaceWithTasks({ assessment, workspace, firstQuestion }: WorkspaceClientProps & { firstQuestion: Question }) {
  const router = useRouter();
  const [activeQuestionId, setActiveQuestionId] = useState(firstQuestion?.question_id ?? "");
  const [questionStates, setQuestionStates] = useState(workspace.questions);
  const questionStatesRef = useRef(workspace.questions);
  const activeQuestion = useMemo(
    () => assessment.questions.find((question) => question.question_id === activeQuestionId) ?? assessment.questions[0],
    [assessment.questions, activeQuestionId]
  );
  const initialState = questionStates[activeQuestionId] ?? createQuestionState(activeQuestion);
  const [language, setLanguage] = useState<Language>(initialState?.selected_language ?? "python");
  const [activeFile, setActiveFile] = useState(initialState?.active_file ?? getFileNames(activeQuestion, language)[0]);
  const [code, setCode] = useState(getCodeFromState(initialState, activeQuestion, initialState?.selected_language ?? "python", activeFile));
  const [saveState, setSaveState] = useState<SaveState>("saved");
  const [runState, setRunState] = useState<"idle" | "running">("idle");
  const [runningQuestionId, setRunningQuestionId] = useState<string | null>(null);
  const [runResults, setRunResults] = useState<Record<string, RunResult | null>>({});
  const [taskErrors, setTaskErrors] = useState<Record<string, string | null>>({});
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [submitState, setSubmitState] = useState<"idle" | "saving" | "submitting">("idle");
  const [error, setError] = useState<string | null>(null);
  const [outputTab, setOutputTab] = useState<"preview" | "console">("preview");
  const [aiMessage, setAiMessage] = useState("");
  const [aiState, setAiState] = useState<"idle" | "running">("idle");
  const aiMessagesEndRef = useRef<HTMLDivElement | null>(null);
  const aiRequestCounterRef = useRef(0);
  const [messages, setMessages] = useState<AiChatMessage[]>([
    { role: "assistant", text: "I am your embedded AI assistant. I can suggest code, explain concepts, or help debug issues. How can I help?" }
  ]);

  const fileNames = useMemo(() => {
    const state = questionStates[activeQuestionId];
    const starterFileNames = getFileNames(activeQuestion, language);
    if (state && Object.keys(state.files).length > 0) {
      return Array.from(new Set([...Object.keys(state.files), ...starterFileNames]));
    }
    return starterFileNames;
  }, [questionStates, activeQuestionId, activeQuestion, language]);

  const visibleLanguages = useMemo(() => getVisibleLanguages(activeQuestion), [activeQuestion]);
  const runResult = runResults[activeQuestionId] ?? null;
  const taskError = taskErrors[activeQuestionId] ?? null;
  const isRunningActiveTask = runState === "running" && runningQuestionId === activeQuestionId;
  const activeTaskIcon = activeQuestion?.task_type ? TASK_ICONS[activeQuestion.task_type] : "file";
  const remainingTime = useRemainingTime(assessment.closes_at);
  const displayRunStatus = runResult ? getDisplayStatus(runResult) : null;
  const runPassedCount = runResult?.test_results.filter((test) => test.passed).length ?? 0;
  const runTotalCount = runResult?.test_results.length ?? 0;
  const isSubmitPending = submitState !== "idle";
  const ideLayout = useWorkspaceIdeLayout();

  useEffect(() => {
    questionStatesRef.current = questionStates;
  }, [questionStates]);

  useEffect(() => {
    aiMessagesEndRef.current?.scrollIntoView({ block: "end", behavior: "smooth" });
  }, [messages, aiState]);

  const persistCurrentCode = useCallback((nextQuestionStates = questionStatesRef.current) => {
    const currentQuestionState = nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, language);
    return {
      ...nextQuestionStates,
      [activeQuestionId]: {
        ...currentQuestionState,
        selected_language: language,
        active_file: activeFile,
        files: {
          ...currentQuestionState.files,
          [activeFile]: { language, content: code }
        },
        last_saved_at: currentQuestionState.last_saved_at ?? new Date().toISOString(),
        version: currentQuestionState.version ?? 0
      }
    };
  }, [activeFile, activeQuestion, activeQuestionId, code, language]);

  useEffect(() => {
    const saving = window.setTimeout(() => setSaveState("saving"), 450);
    const saved = window.setTimeout(async () => {
      try {
        const currentState = persistCurrentCode();
        const stateToSave = currentState[activeQuestionId];
        if (!stateToSave) return;

        const savedWorkspace = await saveWorkspace(assessment.assessment_id, { [activeQuestionId]: stateToSave });
        setQuestionStates((current) => mergeQuestionStates(current, savedWorkspace.questions));
        setSaveState("saved");
      } catch (exception) {
        setError(exception instanceof Error ? exception.message : "Autosave failed.");
        setSaveState("unsaved");
      }
    }, 1300);
    return () => {
      window.clearTimeout(saving);
      window.clearTimeout(saved);
    };
  }, [activeQuestionId, assessment.assessment_id, persistCurrentCode]);

  function updateCode(nextCode: string) {
    setSaveState("unsaved");
    setCode(nextCode);
    setQuestionStates((current) => {
      const currentQuestionState = current[activeQuestionId] ?? createQuestionState(activeQuestion, language);
      return {
        ...current,
        [activeQuestionId]: {
          ...currentQuestionState,
          selected_language: language,
          active_file: activeFile,
          files: {
            ...currentQuestionState.files,
            [activeFile]: { language, content: nextCode }
          }
        }
      };
    });
  }

  function switchFile(nextFile: string) {
    const nextQuestionStates = persistCurrentCode();
    const currentState = nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, language);
    const nextCode = currentState.files[nextFile]?.content ?? getStarterFiles(activeQuestion, language)[nextFile] ?? "";

    setQuestionStates(nextQuestionStates);
    setActiveFile(nextFile);
    setCode(nextCode);
  }

  function switchLanguage(nextLanguage: Language) {
    const nextQuestionStates = persistCurrentCode();
    const starterFiles = getStarterFiles(activeQuestion, nextLanguage);
    const newFileNames = Object.keys(starterFiles);
    const nextFile = newFileNames[0] ?? (nextLanguage === "javascript" ? "main.js" : "main.py");

    const existingState = nextQuestionStates[activeQuestionId];
    const hasFilesForLang = existingState && Object.keys(existingState.files).some(f =>
      newFileNames.includes(f)
    );

    const files: Record<string, { language: Language; content: string }> = {};
    for (const [name, content] of Object.entries(starterFiles)) {
      const existing = hasFilesForLang ? existingState?.files[name] : undefined;
      files[name] = { language: nextLanguage, content: existing?.content ?? content };
    }

    setQuestionStates({
      ...nextQuestionStates,
      [activeQuestionId]: {
        ...(existingState ?? createQuestionState(activeQuestion, nextLanguage)),
        selected_language: nextLanguage,
        active_file: nextFile,
        files: {
          ...(existingState?.files ?? {}),
          ...files
        }
      }
    });
    setLanguage(nextLanguage);
    setActiveFile(nextFile);
    setCode(files[nextFile]?.content ?? "");
  }

  function switchQuestion(question: Question) {
    const nextQuestionStates = persistCurrentCode();
    const nextState = nextQuestionStates[question.question_id] ?? createQuestionState(question);
    const nextLanguage = nextState.selected_language;
    const nextFile = nextState.active_file;

    setQuestionStates({
      ...nextQuestionStates,
      [question.question_id]: nextState
    });
    setActiveQuestionId(question.question_id);
    setLanguage(nextLanguage);
    setActiveFile(nextFile);
    setCode(getCodeFromState(nextState, question, nextLanguage, nextFile));
    setError(null);
  }

  function getAllFiles(): Record<string, string> {
    const currentState = persistCurrentCode();
    const qState = currentState[activeQuestionId];
    if (!qState) return { [activeFile]: code };

    const result: Record<string, string> = {};
    for (const [fileName, content] of Object.entries(getStarterFiles(activeQuestion, language))) {
      result[fileName] = content;
    }
    for (const [fileName, fileData] of Object.entries(qState.files)) {
      if (fileData.language === language) {
        result[fileName] = fileData.content;
      }
    }
    if (Object.keys(result).length === 0) {
      result[activeFile] = code;
    }
    return result;
  }

  async function handleRun() {
    setRunState("running");
    setRunningQuestionId(activeQuestionId);
    setRunResults((current) => ({ ...current, [activeQuestionId]: null }));
    setTaskErrors((current) => ({ ...current, [activeQuestionId]: null }));
    setError(null);
    try {
      const result = await runCode({
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        selected_language: language,
        files: getAllFiles()
      });
      setRunResults((current) => ({ ...current, [activeQuestionId]: result }));
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "Run failed.";
      setTaskErrors((current) => ({ ...current, [activeQuestionId]: message }));
      setError(message);
    } finally {
      setRunState("idle");
      setRunningQuestionId(null);
    }
  }

  async function sendAi(type: AiInteractionType, overrideMessage?: string) {
    if (!assessment.ai_enabled || aiState === "running") {
      return;
    }

    setError(null);
    const message = (overrideMessage ?? aiMessage).trim() || type.replace("_", " ");
    const requestLanguage = language;
    aiRequestCounterRef.current += 1;
    const pendingAssistantId = `pending-ai-${aiRequestCounterRef.current}`;
    setAiState("running");
    setMessages((current) => [
      ...current,
      { role: "student", text: message },
      {
        clientId: pendingAssistantId,
        role: "assistant",
        status: "pending",
        text: "Waiting for the backend to get a real AI provider response..."
      }
    ]);
    setAiMessage("");
    try {
      const response = await getAiResponse({
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        interaction_type: type,
        message,
        selected_language: requestLanguage,
        active_file_content: code,
        active_file_name: activeFile,
        visible_files: getAllFiles(),
        last_run_result: runResults[activeQuestionId] ?? null
      });
      const suggestion = response.suggestion;
      setMessages((current) => current.map((item) =>
        item.clientId === pendingAssistantId
          ? {
            clientId: pendingAssistantId,
            role: "assistant",
            text: response.response_markdown,
            suggestedCode: suggestion?.replacement_code ?? undefined,
            suggestedLanguage: suggestion ? suggestion.language : undefined,
            targetFile: suggestion?.target_file ?? undefined,
            applyLabel: suggestion?.apply_label ?? undefined,
            tokenUsage: response.token_usage
          }
          : item
      ));
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "AI request failed.";
      setMessages((current) => current.map((item) =>
        item.clientId === pendingAssistantId
          ? {
            clientId: pendingAssistantId,
            role: "assistant",
            status: "failed",
            text: `AI request failed: ${message}`
          }
          : item
      ));
      setError(message);
    } finally {
      setAiState("idle");
    }
  }

  function applyAiSuggestion(message: AiChatMessage) {
    if (!message.suggestedCode) {
      return;
    }

    const targetLanguage = message.suggestedLanguage ?? language;
    const targetFile = message.targetFile ?? activeFile;
    const nextQuestionStates = persistCurrentCode();
    const currentQuestionState = nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, targetLanguage);
    const updatedQuestionState: WorkspaceQuestionState = {
      ...currentQuestionState,
      selected_language: targetLanguage,
      active_file: targetFile,
      files: {
        ...currentQuestionState.files,
        [targetFile]: {
          language: targetLanguage,
          content: message.suggestedCode
        }
      }
    };

    setQuestionStates({
      ...nextQuestionStates,
      [activeQuestionId]: updatedQuestionState
    });
    setLanguage(targetLanguage);
    setActiveFile(targetFile);
    setCode(message.suggestedCode);
    setOutputTab("preview");
    setSaveState("unsaved");
  }

  async function submitFinal() {
    if (isSubmitPending) {
      return;
    }

    setConfirmSubmit(false);
    setError(null);
    setSaveState("saving");
    setSubmitState("saving");
    try {
      const nextQuestionStates = persistCurrentCode();
      setQuestionStates(nextQuestionStates);
      const savedWorkspace = await saveWorkspace(assessment.assessment_id, nextQuestionStates);
      setQuestionStates((current) => mergeQuestionStates(current, savedWorkspace.questions));
      setSaveState("saved");
      setSubmitState("submitting");
      await finalizeSubmission(assessment.assessment_id);
      router.push(`/student/assessments/${assessment.assessment_id}/review`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Submission failed.");
      setSaveState("unsaved");
      setSubmitState("idle");
    }
  }

  return (
    <div
      className="relative grid h-[calc(100vh-24px)] min-h-0 min-w-0 overflow-hidden rounded-[18px]"
      style={ideLayout.gridStyle}
    >
      <aside
        className={`panel dynamic-surface flex min-h-0 min-w-0 flex-col rounded-[18px] shadow-[0_18px_44px_rgba(0,0,0,0.24),inset_0_1px_0_rgba(255,255,255,0.08)] ${ideLayout.isTasksCollapsed ? "items-center p-2" : "p-3"}`}
        style={{ gridColumn: "1", gridRow: "1" }}
      >
        {ideLayout.isTasksCollapsed ? (
          <>
            <button
              type="button"
              className="grid h-9 w-9 place-items-center rounded-xl border border-cyanGlow/25 bg-[#0e1726] text-cyanGlow transition hover:border-cyanGlow/60"
              title="Expand tasks panel"
              aria-label="Expand tasks panel"
              onClick={ideLayout.toggleTasksCollapsed}
            >
              <SemanticIcon name="assessments" size={17} />
            </button>
            <span className="mt-3 text-[10px] uppercase tracking-[0.14em] text-white/45 [writing-mode:vertical-rl]">
              Tasks
            </span>
          </>
        ) : (
          <>
        <div className="relative rounded-[16px] border border-white/10 bg-white/[0.035] p-3">
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <p className="text-xs uppercase tracking-[0.16em] text-cyanGlow/70">Assessment tasks</p>
              <h2 className="mt-1 text-base font-semibold leading-snug">{assessment.title}</h2>
            </div>
            <button
              type="button"
              className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-white/10 bg-[#0e1726] text-white/50 transition hover:border-cyanGlow/35 hover:text-cyanGlow"
              title="Collapse tasks panel"
              aria-label="Collapse tasks panel"
              onClick={ideLayout.toggleTasksCollapsed}
            >
              <SemanticIcon name="collapse" size={14} className="rotate-90" />
            </button>
          </div>
          <span className="badge mt-3 hidden w-fit shrink-0 xl:inline-flex">Active attempt</span>
        </div>
        <div className="scrollbar-soft relative mt-3 max-h-[42%] shrink-0 space-y-2 overflow-y-auto pr-1">
          {assessment.questions.map((question, index) => (
            <button
              key={question.question_id}
              onClick={() => switchQuestion(question)}
              className={`dynamic-surface w-full rounded-[14px] border p-2.5 text-left transition ${
                question.question_id === activeQuestionId ? "border-cyanGlow/45 bg-cyanGlow/10 shadow-[0_0_18px_rgba(0,229,255,0.09),inset_0_1px_0_rgba(255,255,255,0.08)]" : "border-white/10 bg-white/[0.045] hover:bg-white/10"
              }`}
            >
              <div className="flex items-start gap-2">
                <span className="mt-0.5 grid h-7 w-7 shrink-0 place-items-center rounded-[10px] border border-white/10 bg-black/20 text-cyanGlow">
                  {(() => {
                    const iconName = question.task_type ? TASK_ICONS[question.task_type] : "file";
                    return <SemanticIcon name={iconName} size={14} />;
                  })()}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="text-xs text-white/40">Task {index + 1}</span>
                  <span className="block truncate font-semibold text-white">{question.title}</span>
                  <span className="mt-2 flex flex-wrap gap-1">
                    <span className="rounded-[7px] border border-white/10 bg-white/5 px-1.5 py-0.5 text-[10px] text-white/55">{formatTaskType(question.task_type)}</span>
                    <span className="rounded-[7px] border border-white/10 bg-white/5 px-1.5 py-0.5 text-[10px] text-white/55">{formatVerificationMode(question.verification_mode)}</span>
                  </span>
                </span>
              </div>
            </button>
          ))}
        </div>
        <div className="scrollbar-soft scanline relative mt-3 min-h-0 flex-1 overflow-y-auto rounded-[16px] border border-white/10 bg-black/20 p-3">
          <p className="text-xs uppercase tracking-[0.16em] text-white/35">Problem statement</p>
          <h3 className="mt-3 text-xl font-semibold text-white">{activeQuestion?.title}</h3>
          <div className="mt-3 flex flex-wrap gap-2">
            <span className="badge"><SemanticIcon name={activeTaskIcon} size={13} /> {formatTaskType(activeQuestion?.task_type)}</span>
            <span className="badge">Difficulty: {formatDifficulty(activeQuestion?.difficulty)}</span>
            <span className="badge">Run: {formatVerificationMode(activeQuestion?.verification_mode)}</span>
          </div>
          <div className="mt-3 space-y-2">{renderMarkdown(activeQuestion?.problem_description_markdown ?? "")}</div>
          <h4 className="mt-6 text-sm font-semibold text-cyanGlow">Constraints</h4>
          {activeQuestion?.constraints.length ? (
            <ul className="mt-2 space-y-2 text-sm text-white/55">
              {activeQuestion.constraints.map((constraint) => <li key={constraint}>- {constraint}</li>)}
            </ul>
          ) : (
            <p className="mt-2 text-sm text-white/40">No extra constraints are listed for this question.</p>
          )}
          <h4 className="mt-6 text-sm font-semibold text-cyanGlow">Public examples</h4>
          {activeQuestion?.public_examples.length ? (
            <div className="mt-2 space-y-2">
              {activeQuestion.public_examples.map((example) => (
                <div key={example.test_case_id} className="rounded-xl border border-white/10 bg-white/5 p-3 text-xs text-white/55">
                  <p className="text-white/80">{example.name}</p>
                </div>
              ))}
            </div>
          ) : (
            <p className="mt-2 text-sm text-white/40">No public examples are listed for this question.</p>
          )}
        </div>
          </>
        )}
      </aside>

      <button
        type="button"
        className="workspace-resizer workspace-resizer-vertical"
        aria-label="Resize tasks panel"
        title="Drag to resize tasks panel"
        disabled={ideLayout.isTasksCollapsed}
        onPointerDown={ideLayout.startResize("tasks")}
        style={{ gridColumn: "2", gridRow: "1" }}
      />

      <section
        className="liquid-glass-neon flex min-h-0 min-w-0 flex-col rounded-xl"
        style={{ gridColumn: "3", gridRow: "1" }}
      >
        <div className="relative flex flex-wrap items-center gap-2 border-b border-white/10 p-3">
          <div className="min-w-0 flex-1">
            <p className="text-xs uppercase tracking-[0.16em] text-white/35">IDE workspace</p>
            <h1 className="truncate text-lg font-semibold xl:text-xl">{activeQuestion?.title ?? assessment.title}</h1>
            <p className="mt-1 truncate text-xs text-white/40">{formatTaskType(activeQuestion?.task_type)} / {formatVerificationMode(activeQuestion?.verification_mode)}</p>
          </div>
          <span className="badge hidden xl:inline-flex"><SemanticIcon name="clock" size={14} /> {remainingTime}</span>
          <span className="badge">{saveState}</span>
          <button className="btn-primary px-4 py-2" onClick={() => setConfirmSubmit(true)} disabled={isSubmitPending}>
            {isSubmitPending ? <Loader2 className="animate-spin" size={16} /> : null}
            {submitState === "saving" ? "Saving..." : submitState === "submitting" ? "Submitting..." : "Submit"}
          </button>
        </div>
        {error ? <p className="relative border-b border-white/10 px-4 py-2 text-sm text-pinkGlow">{error}</p> : null}
        {isSubmitPending ? (
          <p className="relative border-b border-white/10 px-4 py-2 text-sm text-white/55" aria-live="polite">
            {submitState === "saving"
              ? "Saving current workspace before final submission..."
              : "Submitting to backend for hidden-test evaluation. This page will move to review only after the backend confirms."}
          </p>
        ) : null}
        <div className="relative flex flex-wrap items-center gap-2 border-b border-white/10 px-3 py-2">
          <span className="flex items-center gap-1.5 pr-1 text-xs text-white/35"><SemanticIcon name="folder" size={14} /> Files</span>
          <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1">
            {fileNames.map((fileName) => (
              <button
                key={fileName}
                onClick={() => switchFile(fileName)}
                className={`flex max-w-[220px] items-center gap-1.5 rounded-lg border px-3 py-1.5 font-mono text-xs transition ${
                  fileName === activeFile
                    ? "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow shadow-[0_0_18px_rgba(0,229,255,0.06)]"
                    : "border-white/10 bg-white/5 text-white/45 hover:bg-white/10 hover:text-white/70"
                }`}
                title={fileName}
              >
                <SemanticIcon name="file" size={13} className="shrink-0" />
                <span className="truncate">{fileName}</span>
              </button>
            ))}
          </div>
          <label className="ml-auto hidden text-xs text-white/40 xl:inline" htmlFor="language">Language</label>
          <select id="language" className="field py-2" value={language} onChange={(event) => switchLanguage(event.target.value as Language)}>
            {STUDENT_LANGUAGES.filter((option) => visibleLanguages.includes(option.value)).map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
          <button className="btn-secondary px-4 py-2" onClick={handleRun} disabled={runState === "running"}>
            <SemanticIcon name="play" size={16} />
            {isRunningActiveTask ? "Running..." : "Run"}
          </button>
        </div>
        <div className="relative min-h-0 flex-1 overflow-hidden bg-black/30 p-3">
          <MonacoCodeEditor
            assessmentId={assessment.assessment_id}
            questionId={activeQuestionId}
            fileName={activeFile}
            language={language}
            value={code}
            onChange={updateCode}
          />
        </div>
      </section>

      <button
        type="button"
        className="workspace-resizer workspace-resizer-vertical"
        aria-label="Resize AI panel"
        title="Drag to resize AI panel"
        disabled={ideLayout.isAgentCollapsed}
        onPointerDown={ideLayout.startResize("agent")}
        style={{ gridColumn: "4", gridRow: "1" }}
      />

      <aside
        className={`panel dynamic-surface flex min-h-0 min-w-0 flex-col rounded-xl ${ideLayout.isAgentCollapsed ? "items-center p-2" : "p-3"}`}
        style={{ gridColumn: "5", gridRow: "1" }}
      >
        {ideLayout.isAgentCollapsed ? (
          <>
            <button
              type="button"
              className="grid h-9 w-9 place-items-center rounded-xl border border-cyanGlow/25 bg-[#0e1726] text-cyanGlow transition hover:border-cyanGlow/60"
              title="Expand AI panel"
              aria-label="Expand AI panel"
              onClick={ideLayout.toggleAgentCollapsed}
            >
              <SemanticIcon name="ai" size={18} />
            </button>
            <span className="mt-3 text-[10px] uppercase tracking-[0.14em] text-white/45 [writing-mode:vertical-rl]">
              AI Agent
            </span>
          </>
        ) : (
          <>
        <div className="relative flex items-center gap-3">
          <span className="float-soft grid h-10 w-10 place-items-center rounded-2xl border border-cyanGlow/20 bg-[linear-gradient(145deg,rgba(0,229,255,0.14),rgba(168,85,247,0.16))] text-cyanGlow">
            <SemanticIcon name="ai" size={22} />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="font-semibold">AI Agent</h2>
            <p className="text-xs text-white/40">{assessment.ai_enabled ? "Available for this assessment" : "Disabled for this assessment"}</p>
          </div>
          <button
            type="button"
            className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-white/10 bg-[#0e1726] text-white/50 transition hover:border-cyanGlow/35 hover:text-cyanGlow"
            title="Collapse AI panel"
            aria-label="Collapse AI panel"
            onClick={ideLayout.toggleAgentCollapsed}
          >
            <SemanticIcon name="collapse" size={14} className="-rotate-90" />
          </button>
        </div>
        <div className="relative mt-4 grid grid-cols-1 gap-2 xl:grid-cols-2">
          {(["code_suggestion", "explanation", "debugging"] as AiInteractionType[]).map((type) => (
            <button
              key={type}
              disabled={!assessment.ai_enabled || aiState === "running"}
              className="btn-secondary justify-center px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-45"
              onClick={() => sendAi(type)}
            >
              <span className="flex min-w-0 items-center gap-2">
                <SemanticIcon name={AI_ACTION_ICONS[type]} size={14} className="shrink-0" />
                <span className="block truncate">{type.replace("_", " ")}</span>
              </span>
            </button>
          ))}
        </div>
        <div className="scrollbar-soft relative mt-4 min-h-0 flex-1 space-y-3 overflow-y-auto rounded-xl border border-white/10 bg-black/20 p-3">
          {messages.map((message, index) => (
            <div key={message.clientId ?? `${message.role}-${index}`} className={`reveal-up rounded-2xl p-3 text-sm leading-6 ${message.role === "assistant" ? "bg-white/5 text-white/70" : "bg-cyanGlow/10 text-cyanGlow"}`}>
              {message.status === "pending" ? (
                <span className="mb-2 inline-flex items-center gap-2 text-xs text-white/45">
                  <Loader2 className="animate-spin" size={13} />
                  Provider request in progress
                </span>
              ) : null}
              {message.status === "failed" ? (
                <span className="mb-2 inline-flex items-center gap-2 text-xs text-pinkGlow">
                  Backend/provider error
                </span>
              ) : null}
              {renderMarkdown(message.text)}
              {message.role === "assistant" && message.suggestedCode ? (
                <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-white/10 pt-3">
                  <button
                    type="button"
                    className="btn-primary px-3 py-2 text-xs"
                    onClick={() => applyAiSuggestion(message)}
                  >
                    <SemanticIcon name="file" size={13} />
                    {message.applyLabel ?? `Apply to ${message.targetFile ?? activeFile}`}
                  </button>
                  <span className="text-[11px] text-white/35">Review, then run public checks.</span>
                </div>
              ) : null}
              {message.role === "assistant" && message.tokenUsage ? (
                <p className="mt-2 text-[10px] uppercase tracking-[0.12em] text-white/30">
                  Tokens {message.tokenUsage.total_tokens}
                </p>
              ) : null}
            </div>
          ))}
          <div ref={aiMessagesEndRef} />
        </div>
        <div className="relative mt-4 flex gap-2 rounded-2xl border border-white/10 bg-black/20 p-2">
          <textarea
            className="max-h-24 min-h-10 min-w-0 flex-1 resize-none bg-transparent px-2 py-2 text-sm text-white outline-none placeholder:text-white/30 disabled:cursor-not-allowed disabled:opacity-45"
            placeholder={assessment.ai_enabled ? "Ask a question..." : "AI disabled for this assessment"}
            value={aiMessage}
            disabled={!assessment.ai_enabled || aiState === "running"}
            onChange={(event) => setAiMessage(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void sendAi("code_suggestion");
              }
            }}
          />
          <button
            className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-cyanGlow text-slate-950 disabled:cursor-not-allowed disabled:opacity-45"
            disabled={!assessment.ai_enabled || aiState === "running"}
            onClick={() => sendAi("code_suggestion")}
            aria-label="Send AI request"
          >
            <SemanticIcon name="send" size={16} />
          </button>
        </div>
          </>
        )}
      </aside>

      <button
        type="button"
        className="workspace-resizer workspace-resizer-horizontal"
        aria-label="Resize output panel"
        title="Drag to resize output panel"
        disabled={ideLayout.isOutputCollapsed}
        onPointerDown={ideLayout.startResize("output")}
        style={{ gridColumn: "1 / 6", gridRow: "2" }}
      />

      <section
        className="flex min-h-0 min-w-0 flex-col overflow-hidden rounded-[16px] border border-cyanGlow/45 bg-[#07111d] shadow-[0_-16px_38px_rgba(0,0,0,0.32),inset_0_1px_0_rgba(255,255,255,0.08)]"
        style={{ gridColumn: "1 / 6", gridRow: "3" }}
      >
        <div className="flex shrink-0 items-center border-b border-white/10 bg-[#0b1220] px-3">
          <button
            onClick={() => setOutputTab("preview")}
            className={`flex items-center gap-1.5 border-b-2 px-3 py-2 text-xs font-medium transition ${
              outputTab === "preview"
                ? "border-cyanGlow text-cyanGlow"
                : "border-transparent text-white/40 hover:text-white/60"
            }`}
          >
            <SemanticIcon name="preview" size={13} />
            Preview
          </button>
          <button
            onClick={() => setOutputTab("console")}
            className={`flex items-center gap-1.5 border-b-2 px-3 py-2 text-xs font-medium transition ${
              outputTab === "console"
                ? "border-cyanGlow text-cyanGlow"
                : "border-transparent text-white/40 hover:text-white/60"
            }`}
          >
            <SemanticIcon name="console" size={13} />
            Console
            {runResult && (
              <span className={`ml-1 rounded-full px-1.5 py-0.5 text-[10px] ${
                runResult.status === "passed" ? "bg-emerald-500/20 text-emerald-300" : "bg-rose-500/20 text-rose-300"
              }`}>
                {runResult.test_results.filter(t => t.passed).length}/{runResult.test_results.length}
              </span>
            )}
          </button>
          <button
            className="ml-2 rounded-lg border border-white/10 bg-white/5 p-1.5 text-white/45 transition hover:border-cyanGlow/30 hover:text-cyanGlow"
            title={ideLayout.isOutputCollapsed ? "Expand output" : "Collapse output"}
            aria-label={ideLayout.isOutputCollapsed ? "Expand output panel" : "Collapse output panel"}
            type="button"
            onClick={ideLayout.toggleOutputCollapsed}
          >
            <SemanticIcon name={ideLayout.isOutputCollapsed ? "expand" : "collapse"} size={14} />
          </button>
          <button
            className="ml-1 rounded-lg border border-white/10 bg-white/5 p-1.5 text-white/45 transition hover:border-cyanGlow/30 hover:text-cyanGlow"
            title="Reset IDE layout"
            aria-label="Reset IDE layout"
            type="button"
            onClick={ideLayout.resetLayout}
          >
            <SemanticIcon name="settings" size={14} />
          </button>
          <div className="ml-auto flex min-w-0 items-center gap-2 text-[10px] text-white/30">
            {runResult && displayRunStatus ? (
              <>
                <span className={`hidden rounded-full border px-2 py-0.5 font-semibold sm:inline-flex ${getStatusClass(displayRunStatus)}`}>
                  {formatExecutionStatus(displayRunStatus)}
                </span>
                <span className="hidden rounded-full border border-white/10 bg-white/5 px-2 py-0.5 text-white/50 sm:inline-flex">
                  {runPassedCount}/{runTotalCount} public checks
                </span>
                <span className="hidden rounded-full border border-white/10 bg-white/5 px-2 py-0.5 text-white/35 lg:inline-flex">
                  CPU {runResult.metrics.cpu_time_seconds}s / {runResult.metrics.peak_memory_kb}kb
                </span>
              </>
            ) : null}
            <span className="hidden sm:inline">Docked output panel</span>
            <span className="shrink-0">Public/sample tests only</span>
          </div>
        </div>
        <div className={`${ideLayout.isOutputCollapsed ? "hidden" : "min-h-0 flex-1 overflow-hidden bg-[#07111d] p-2"}`}>
          {outputTab === "preview" ? (
            <TaskVerificationPreview
              question={activeQuestion}
              runResult={runResult}
              runState={isRunningActiveTask ? "running" : "idle"}
            />
          ) : (
            <ConsolePanel isRunning={isRunningActiveTask} runResult={runResult} taskError={taskError} />
          )}
        </div>
      </section>

      {confirmSubmit ? (
        <Modal title="Submit final solution" onClose={() => setConfirmSubmit(false)}>
          <p className="text-white/60">This submits the current attempt. Hidden test input and expected output remain private.</p>
          <div className="mt-6 flex justify-end gap-3">
            <button className="btn-secondary" onClick={() => setConfirmSubmit(false)}>Cancel</button>
            <button className="btn-primary" onClick={submitFinal} disabled={isSubmitPending}>
              {isSubmitPending ? <Loader2 className="animate-spin" size={16} /> : null}
              {submitState === "saving" ? "Saving..." : submitState === "submitting" ? "Submitting..." : "Submit result"}
            </button>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}

function Modal({ title, children, onClose }: { title: string; children: React.ReactNode; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/70 p-4 backdrop-blur-sm">
      <section className="liquid-glass-neon w-full max-w-xl rounded-3xl p-6">
        <div className="relative flex items-center justify-between gap-4">
          <h2 className="text-2xl font-semibold">{title}</h2>
          <button className="rounded-xl border border-white/10 bg-white/5 p-2 text-white/60 hover:text-white" onClick={onClose}><SemanticIcon name="close" size={18} /></button>
        </div>
        <div className="relative mt-4">{children}</div>
      </section>
    </div>
  );
}
