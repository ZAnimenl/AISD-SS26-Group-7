"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, PanelLeftClose, PanelLeftOpen, PanelRightClose, PanelRightOpen, PanelBottomClose, PanelBottomOpen, ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { finalizeSubmission, getAiResponse, getAiTaskTranscript, getAiUsage, recordAiInteractionEvent, runCode, saveWorkspace } from "@/lib/api";
import { MonacoCodeEditor } from "@/components/workspace/MonacoCodeEditor";
import { TaskVerificationPreview } from "@/components/workspace/previews/TaskVerificationPreview";
import { ConsolePanel, formatExecutionStatus, getDisplayStatus, getStatusClass } from "@/components/workspace/ConsolePanel";
import { renderMarkdown } from "@/components/workspace/workspaceMarkdown";
import { useWorkspaceIdeLayout } from "@/components/workspace/useWorkspaceIdeLayout";
import { SemanticIcon, type SemanticIconName } from "@/components/ui/SemanticIcon";
import { CustomDropdown } from "@/components/ui/CustomDropdown";
import { getDefaultFileNameForLanguage, normalizeStudentLanguageConstraints, STUDENT_LANGUAGE_OPTIONS } from "@/lib/languages";
import type { AiInteractionType, AiWorkspaceAction, Assessment, Language, Question, RunResult, TaskType, VerificationMode, WorkspaceQuestionState, WorkspaceState } from "@/lib/types";

interface WorkspaceClientProps {
  assessment: Assessment;
  workspace: WorkspaceState;
  sandboxAvailable: boolean;
}

type SaveState = "saved" | "unsaved" | "saving";
type AiAgentPage = "chat" | "usage";

type AiChatMessage = {
  clientId?: string;
  interactionId?: string;
  responseVisibleAt?: number;
  role: "assistant" | "student";
  text: string;
  status?: "pending" | "failed";
  suggestedCode?: string;
  suggestedLanguage?: Language;
  targetFile?: string;
  applyLabel?: string;
  workspaceActions?: AiWorkspaceAction[];
  tokenUsage?: {
    input_tokens: number;
    output_tokens: number;
    total_tokens: number;
  };
};

type WorkspaceAiUsageSummary = {
  total_interactions: number;
  total_tokens: number;
  average_tokens_per_interaction: number;
  by_question?: Record<string, {
    total_interactions: number;
    total_tokens: number;
    token_efficiency?: {
      prompt_source: {
        characters_per_token: number;
        tokens_per_character: number;
      };
      response: {
        characters_per_token: number;
        tokens_per_character: number;
      };
      context_signals_provided: number;
      required_context_signals: number;
    };
  }>;
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

const SANDBOX_UNAVAILABLE_MESSAGE = "Run and submit are temporarily unavailable. Please try again later or contact an administrator.";
const MAX_PROBLEM_STATEMENT_WORDS = 150;

function createInitialAiMessages(): AiChatMessage[] {
  return [{ role: "assistant", text: "I am your embedded AI assistant. I can suggest code, explain concepts, or help debug issues. How can I help?" }];
}

function limitProblemStatement(markdown: string) {
  const words = Array.from(markdown.matchAll(/\S+/g));
  if (words.length <= MAX_PROBLEM_STATEMENT_WORDS) {
    return markdown;
  }

  const finalWord = words[MAX_PROBLEM_STATEMENT_WORDS - 1];
  return `${markdown.slice(0, (finalWord.index ?? 0) + finalWord[0].length).trimEnd()}…`;
}

function getMessageReplaceFileActions(message: AiChatMessage, fallbackFile: string, fallbackLanguage: Language): AiWorkspaceAction[] {
  const actions = message.workspaceActions?.filter((item) => item.type === "replace_file" && item.replacement_code) ?? [];
  if (actions.length > 0) {
    return actions;
  }

  return message.suggestedCode
    ? [{
      type: "replace_file",
      label: message.applyLabel ?? `Apply to ${message.targetFile ?? fallbackFile}`,
      target_file: message.targetFile ?? fallbackFile,
      language: message.suggestedLanguage ?? fallbackLanguage,
      replacement_code: message.suggestedCode
    }]
    : [];
}

function messageHasRunPublicChecksAction(message: AiChatMessage) {
  return message.workspaceActions?.some((action) => action.type === "run_public_checks") ?? false;
}

function AiMessageActionButtons({
  message,
  activeFile,
  language,
  isApplying,
  isRunning,
  canRun,
  onExecute,
  onReject
}: {
  message: AiChatMessage;
  activeFile: string;
  language: Language;
  isApplying: boolean;
  isRunning: boolean;
  canRun: boolean;
  onExecute: (runAfterApply: boolean, eventTimestamp: number) => void;
  onReject: (eventTimestamp: number) => void;
}) {
  const replaceActions = getMessageReplaceFileActions(message, activeFile, language);
  const hasReplaceActions = replaceActions.length > 0;
  const canRunFromAgent = hasReplaceActions || messageHasRunPublicChecksAction(message);
  if (!hasReplaceActions && !canRunFromAgent) {
    return null;
  }
  const targetLabel = replaceActions
    .map((action) => action.target_file)
    .filter((targetFile): targetFile is string => Boolean(targetFile))
    .join(", ");

  return (
    <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-white/10 pt-3">
      {hasReplaceActions ? (
        <>
          <p className="w-full text-xs leading-5 text-white/50">
            Review the proposed file changes before choosing whether to apply them.
          </p>
          <button
            type="button"
            className="btn-primary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-45"
            disabled={isApplying}
            onClick={(event) => onExecute(false, event.timeStamp)}
          >
            <SemanticIcon name="file" size={13} />
            Apply changes
          </button>
        </>
      ) : null}
      {hasReplaceActions ? (
        <button
          type="button"
          className="btn-secondary px-3 py-2 text-xs"
          disabled={isApplying}
          onClick={(event) => onReject(event.timeStamp)}
        >
          Reject suggestion
        </button>
      ) : null}
      {canRunFromAgent ? (
        <button
          type="button"
          className="btn-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-45"
          disabled={isApplying || isRunning || !canRun}
          onClick={(event) => onExecute(hasReplaceActions, event.timeStamp)}
        >
          <SemanticIcon name="play" size={13} />
          {canRun ? (hasReplaceActions ? "Apply & run" : "Run public checks") : "Run unavailable"}
        </button>
      ) : null}
      {targetLabel ? (
        <span className="font-mono text-[11px] text-white/35">{targetLabel}</span>
      ) : null}
    </div>
  );
}

function getDefaultFileName(language: Language) {
  return getDefaultFileNameForLanguage(language);
}

function getVisibleLanguages(question: Question | undefined) {
  return normalizeStudentLanguageConstraints(question?.language_constraints, question?.task_type);
}

function resolveAllowedLanguage(question: Question | undefined, language?: Language) {
  const visibleLanguages = getVisibleLanguages(question);
  return language && visibleLanguages.includes(language) ? language : visibleLanguages[0] ?? "python";
}

function getStarterFiles(question: Question | undefined, language: Language): Record<string, string> {
  const starterFiles = question?.starter_code[language] ?? {};
  if (Object.keys(starterFiles).length > 0) {
    return starterFiles;
  }

  if (language === "html") {
    return question?.starter_code.javascript ?? {};
  }

  return {};
}

function getFileNames(question: Question | undefined, language: Language, state?: WorkspaceQuestionState): string[] {
  const files = getStarterFiles(question, language);
  const savedNames = Object.entries(state?.files ?? {})
    .filter(([, file]) => file.language === language)
    .map(([fileName]) => fileName);
  const names = Array.from(new Set([...savedNames, ...Object.keys(files)]));
  return names.length > 0 ? names : [getDefaultFileName(language)];
}

function sanitizeQuestionState(question: Question | undefined, state?: WorkspaceQuestionState, preferredLanguage?: Language): WorkspaceQuestionState {
  const language = resolveAllowedLanguage(question, preferredLanguage ?? state?.selected_language);
  const starterFiles = getStarterFiles(question, language);
  const allowedLanguages = new Set(getVisibleLanguages(question));
  const files: Record<string, { language: Language; content: string }> = {};

  for (const [fileName, file] of Object.entries(state?.files ?? {})) {
    if (allowedLanguages.has(file.language)) {
      files[fileName] = file;
    }
  }

  for (const [fileName, content] of Object.entries(starterFiles)) {
    const existing = files[fileName];
    files[fileName] = existing?.language === language ? existing : { language, content };
  }

  const languageFileNames = Object.entries(files)
    .filter(([, file]) => file.language === language)
    .map(([fileName]) => fileName);
  const activeFile = state?.active_file && languageFileNames.includes(state.active_file)
    ? state.active_file
    : languageFileNames[0] ?? getDefaultFileName(language);

  if (!files[activeFile] || files[activeFile].language !== language) {
    files[activeFile] = { language, content: starterFiles[activeFile] ?? "" };
  }

  return {
    selected_language: language,
    active_file: activeFile,
    files,
    last_saved_at: state?.last_saved_at ?? "",
    version: state?.version ?? 0
  };
}

function createQuestionState(question: Question | undefined, language?: Language): WorkspaceQuestionState {
  return sanitizeQuestionState(question, undefined, language);
}

function normalizeWorkspaceQuestionStates(
  questions: Question[],
  savedStates: WorkspaceState["questions"]
): WorkspaceState["questions"] {
  return questions.reduce<WorkspaceState["questions"]>((states, question) => ({
    ...states,
    [question.question_id]: sanitizeQuestionState(question, savedStates[question.question_id])
  }), {});
}

function getCodeFromState(state: WorkspaceQuestionState | undefined, question: Question | undefined, language: Language, fileName?: string) {
  const targetFile = fileName ?? state?.active_file ?? getFileNames(question, language, state)[0];
  const savedFile = state?.files[targetFile];
  if (savedFile?.language === language) {
    return savedFile.content;
  }
  const starterFiles = getStarterFiles(question, language);
  return starterFiles[targetFile] ?? "";
}

function getFilesFromQuestionState(question: Question | undefined, state: WorkspaceQuestionState): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [fileName, content] of Object.entries(getStarterFiles(question, state.selected_language))) {
    result[fileName] = content;
  }
  for (const [fileName, fileData] of Object.entries(state.files)) {
    if (fileData.language === state.selected_language) {
      result[fileName] = fileData.content;
    }
  }
  if (Object.keys(result).length === 0) {
    result[state.active_file] = getCodeFromState(state, question, state.selected_language, state.active_file);
  }
  return result;
}

function isAiActionLanguageAllowed(question: Question | undefined, selectedLanguage: Language, targetFile?: string | null, actionLanguage?: string | null) {
  if (!actionLanguage) {
    return true;
  }

  if (getVisibleLanguages(question).includes(actionLanguage as Language)) {
    return true;
  }

  if (selectedLanguage !== "html") {
    return false;
  }

  const extension = targetFile?.split(".").pop()?.toLowerCase();
  return extension === "js" && actionLanguage === "javascript"
    || extension === "css" && actionLanguage === "css"
    || extension === "json" && actionLanguage === "json"
    || extension === "html" && actionLanguage === "html";
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

function formatTokenCount(value: number) {
  return value.toLocaleString();
}

function useRemainingTime(expiresAt: string) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  return formatRemainingTime(expiresAt, now);
}

export function WorkspaceClient({ assessment, workspace, sandboxAvailable }: WorkspaceClientProps) {
  const firstQuestion = assessment.questions[0];
  if (!firstQuestion) {
    return <EmptyTaskWorkspace />;
  }

  return <WorkspaceWithTasks assessment={assessment} workspace={workspace} firstQuestion={firstQuestion} sandboxAvailable={sandboxAvailable} />;
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

function WorkspaceWithTasks({ assessment, workspace, firstQuestion, sandboxAvailable }: WorkspaceClientProps & { firstQuestion: Question }) {
  const router = useRouter();
  const [activeQuestionId, setActiveQuestionId] = useState(firstQuestion?.question_id ?? "");
  const initialQuestionStates = useMemo(
    () => normalizeWorkspaceQuestionStates(assessment.questions, workspace.questions),
    [assessment.questions, workspace.questions]
  );
  const [questionStates, setQuestionStates] = useState(initialQuestionStates);
  const questionStatesRef = useRef(initialQuestionStates);
  const activeQuestion = useMemo(
    () => assessment.questions.find((question) => question.question_id === activeQuestionId) ?? assessment.questions[0],
    [assessment.questions, activeQuestionId]
  );
  const activeQuestionIndex = Math.max(
    0,
    assessment.questions.findIndex((question) => question.question_id === activeQuestion?.question_id)
  );
  const initialState = sanitizeQuestionState(activeQuestion, initialQuestionStates[activeQuestionId]);
  const [language, setLanguage] = useState<Language>(initialState.selected_language);
  const [activeFile, setActiveFile] = useState(initialState.active_file);
  const [code, setCode] = useState(getCodeFromState(initialState, activeQuestion, initialState.selected_language, initialState.active_file));
  const [saveState, setSaveState] = useState<SaveState>("saved");
  const [runState, setRunState] = useState<"idle" | "running">("idle");
  const [runningQuestionId, setRunningQuestionId] = useState<string | null>(null);
  const [runResults, setRunResults] = useState<Record<string, RunResult | null>>({});
  const [taskErrors, setTaskErrors] = useState<Record<string, string | null>>({});
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [submitState, setSubmitState] = useState<"idle" | "saving" | "submitting">("idle");
  const [workspaceFrozen, setWorkspaceFrozen] = useState(false);
  const [frozenTimeLabel, setFrozenTimeLabel] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [outputTab, setOutputTab] = useState<"preview" | "console">("preview");
  const [aiAgentPage, setAiAgentPage] = useState<AiAgentPage>("chat");
  const [aiMessagesByQuestion, setAiMessagesByQuestion] = useState<Record<string, AiChatMessage[]>>({
    [firstQuestion.question_id]: createInitialAiMessages()
  });
  const [aiDraftsByQuestion, setAiDraftsByQuestion] = useState<Record<string, string>>({});
  const [aiState, setAiState] = useState<"idle" | "running">("idle");
  const [agentActionState, setAgentActionState] = useState<"idle" | "applying">("idle");
  const [aiUsageSummary, setAiUsageSummary] = useState<WorkspaceAiUsageSummary | null>(null);
  const aiMessagesEndRef = useRef<HTMLDivElement | null>(null);
  const aiRequestCounterRef = useRef(0);
  const loadedAiTranscriptsRef = useRef(new Set<string>());
  const appliedSuggestionRef = useRef<{ interactionId: string; code: string; appliedAt: number } | null>(null);
  const messages = aiMessagesByQuestion[activeQuestionId] ?? createInitialAiMessages();
  const aiMessage = aiDraftsByQuestion[activeQuestionId] ?? "";

  const fileNames = useMemo(() => {
    const state = sanitizeQuestionState(activeQuestion, questionStates[activeQuestionId], language);
    return getFileNames(activeQuestion, state.selected_language, state);
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
  const activeTaskAiUsage = aiUsageSummary?.by_question?.[activeQuestionId] ?? { total_interactions: 0, total_tokens: 0 };

  useEffect(() => {
    questionStatesRef.current = questionStates;
  }, [questionStates]);

  useEffect(() => {
    aiMessagesEndRef.current?.scrollIntoView({ block: "end", behavior: "smooth" });
  }, [messages, aiState]);

  useEffect(() => {
    if (!assessment.ai_enabled || loadedAiTranscriptsRef.current.has(activeQuestionId)) {
      return;
    }

    let isMounted = true;
    loadedAiTranscriptsRef.current.add(activeQuestionId);
    getAiTaskTranscript(assessment.assessment_id, activeQuestionId)
      .then((transcript) => {
        if (!isMounted) return;
        const restoredMessages = transcript.interactions.flatMap((interaction) => [
          { clientId: `input-${interaction.interaction_id}`, role: "student" as const, text: interaction.input },
          {
            clientId: `output-${interaction.interaction_id}`,
            interactionId: interaction.interaction_id,
            role: "assistant" as const,
            text: interaction.output,
            tokenUsage: interaction.token_usage
          }
        ]);
        setAiMessagesByQuestion((current) => ({
          ...current,
          [activeQuestionId]: restoredMessages.length > 0 ? restoredMessages : (current[activeQuestionId] ?? createInitialAiMessages())
        }));
      })
      .catch(() => undefined);
    return () => {
      isMounted = false;
    };
  }, [activeQuestionId, assessment.ai_enabled, assessment.assessment_id]);

  useEffect(() => {
    if (!assessment.ai_enabled) {
      return;
    }

    let isMounted = true;
    getAiUsage(assessment.assessment_id)
      .then((usage) => {
        if (!isMounted) {
          return;
        }

        setAiUsageSummary({
          total_interactions: usage.total_interactions,
          total_tokens: usage.total_tokens,
          average_tokens_per_interaction: usage.average_tokens_per_interaction,
          by_question: usage.by_question ?? {}
        });
      })
      .catch(() => {
        if (isMounted) {
          setAiUsageSummary(null);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [assessment.ai_enabled, assessment.assessment_id]);

  const persistCurrentCode = useCallback((nextQuestionStates = questionStatesRef.current) => {
    const selectedLanguage = resolveAllowedLanguage(activeQuestion, language);
    const currentQuestionState = sanitizeQuestionState(
      activeQuestion,
      nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, selectedLanguage),
      selectedLanguage
    );
    const currentFile = currentQuestionState.active_file === activeFile || currentQuestionState.files[activeFile]?.language === selectedLanguage
      ? activeFile
      : currentQuestionState.active_file;
    return {
      ...nextQuestionStates,
      [activeQuestionId]: {
        ...currentQuestionState,
        selected_language: selectedLanguage,
        active_file: currentFile,
        files: {
          ...currentQuestionState.files,
          [currentFile]: { language: selectedLanguage, content: code }
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
        setQuestionStates((current) => normalizeWorkspaceQuestionStates(
          assessment.questions,
          mergeQuestionStates(current, savedWorkspace.questions)
        ));
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
  }, [activeQuestionId, assessment.assessment_id, assessment.questions, persistCurrentCode]);

  function updateCode(nextCode: string, eventTimestamp: number) {
    if (workspaceFrozen) return;
    const appliedSuggestion = appliedSuggestionRef.current;
    if (appliedSuggestion && nextCode !== appliedSuggestion.code) {
      void recordAiInteractionEvent({
        assessment_id: assessment.assessment_id,
        interaction_id: appliedSuggestion.interactionId,
        event_type: "edit",
        elapsed_milliseconds: Math.max(0, Math.round(eventTimestamp - appliedSuggestion.appliedAt)),
        applied_unchanged: false
      }).catch(() => undefined);
      appliedSuggestionRef.current = null;
    }
    setSaveState("unsaved");
    setCode(nextCode);
    setQuestionStates((current) => {
      const selectedLanguage = resolveAllowedLanguage(activeQuestion, language);
      const currentQuestionState = sanitizeQuestionState(
        activeQuestion,
        current[activeQuestionId] ?? createQuestionState(activeQuestion, selectedLanguage),
        selectedLanguage
      );
      return {
        ...current,
        [activeQuestionId]: {
          ...currentQuestionState,
          selected_language: selectedLanguage,
          active_file: activeFile,
          files: {
            ...currentQuestionState.files,
            [activeFile]: { language: selectedLanguage, content: nextCode }
          }
        }
      };
    });
  }

  function switchFile(nextFile: string) {
    if (workspaceFrozen) return;
    const nextQuestionStates = persistCurrentCode();
    const currentState = sanitizeQuestionState(activeQuestion, nextQuestionStates[activeQuestionId], language);
    const nextCode = getCodeFromState(currentState, activeQuestion, currentState.selected_language, nextFile);

    setQuestionStates({
      ...nextQuestionStates,
      [activeQuestionId]: currentState
    });
    setActiveFile(nextFile);
    setCode(nextCode);
  }

  function switchLanguage(nextLanguage: Language) {
    if (workspaceFrozen) return;
    const nextQuestionStates = persistCurrentCode();
    const selectedLanguage = resolveAllowedLanguage(activeQuestion, nextLanguage);
    const nextState = sanitizeQuestionState(activeQuestion, nextQuestionStates[activeQuestionId], selectedLanguage);

    setQuestionStates({
      ...nextQuestionStates,
      [activeQuestionId]: nextState
    });
    setLanguage(nextState.selected_language);
    setActiveFile(nextState.active_file);
    setCode(getCodeFromState(nextState, activeQuestion, nextState.selected_language, nextState.active_file));
  }

  function switchQuestion(question: Question) {
    if (workspaceFrozen) return;
    const nextQuestionStates = persistCurrentCode();
    const nextState = sanitizeQuestionState(question, nextQuestionStates[question.question_id]);
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

  function moveQuestion(direction: -1 | 1) {
    const nextQuestion = assessment.questions[activeQuestionIndex + direction];
    if (nextQuestion) {
      switchQuestion(nextQuestion);
    }
  }

  function getAllFiles(): Record<string, string> {
    const currentState = persistCurrentCode();
    const qState = sanitizeQuestionState(activeQuestion, currentState[activeQuestionId], language);
    return getFilesFromQuestionState(activeQuestion, qState);
  }

  async function runPublicChecksForState(state: WorkspaceQuestionState) {
    if (!sandboxAvailable) {
      setTaskErrors((current) => ({ ...current, [activeQuestionId]: SANDBOX_UNAVAILABLE_MESSAGE }));
      setError(SANDBOX_UNAVAILABLE_MESSAGE);
      return null;
    }

    setRunState("running");
    setRunningQuestionId(activeQuestionId);
    setRunResults((current) => ({ ...current, [activeQuestionId]: null }));
    setTaskErrors((current) => ({ ...current, [activeQuestionId]: null }));
    setError(null);
    try {
      const result = await runCode({
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        selected_language: state.selected_language,
        files: getFilesFromQuestionState(activeQuestion, state)
      });
      setRunResults((current) => ({ ...current, [activeQuestionId]: result }));
      return result;
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "Run failed.";
      setTaskErrors((current) => ({ ...current, [activeQuestionId]: message }));
      setError(message);
      return null;
    } finally {
      setRunState("idle");
      setRunningQuestionId(null);
    }
  }

  async function handleRun() {
    if (workspaceFrozen) return;
    const selectedLanguage = resolveAllowedLanguage(activeQuestion, language);
    const currentState = sanitizeQuestionState(activeQuestion, persistCurrentCode()[activeQuestionId], selectedLanguage);
    await runPublicChecksForState(currentState);
  }

  async function sendAi(type: AiInteractionType, overrideMessage?: string) {
    if (workspaceFrozen || !assessment.ai_enabled || aiState === "running") {
      return;
    }

    setError(null);
    const questionId = activeQuestionId;
    const message = (overrideMessage ?? aiMessage).trim() || type.replace("_", " ");
    const requestLanguage = resolveAllowedLanguage(activeQuestion, language);
    const currentState = sanitizeQuestionState(activeQuestion, questionStatesRef.current[activeQuestionId], requestLanguage);
    const currentFile = currentState.files[activeFile]?.language === requestLanguage ? activeFile : currentState.active_file;
    const currentCode = currentFile === activeFile ? code : getCodeFromState(currentState, activeQuestion, requestLanguage, currentFile);
    aiRequestCounterRef.current += 1;
    const pendingAssistantId = `pending-ai-${aiRequestCounterRef.current}`;
    setAiState("running");
    setAiMessagesByQuestion((current) => ({
      ...current,
      [questionId]: [
        ...(current[questionId] ?? createInitialAiMessages()),
        { role: "student", text: message },
        {
          clientId: pendingAssistantId,
          role: "assistant",
          status: "pending",
          text: "Preparing a response..."
        }
      ]
    }));
    setAiDraftsByQuestion((current) => ({ ...current, [questionId]: "" }));
    try {
      const response = await getAiResponse({
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        interaction_type: type,
        message,
        selected_language: requestLanguage,
        active_file_content: currentCode,
        active_file_name: currentFile,
        visible_files: getAllFiles(),
        last_run_result: runResults[activeQuestionId] ?? null
      });
      setAiUsageSummary((current) => {
        const totalInteractions = (current?.total_interactions ?? 0) + 1;
        const totalTokens = (current?.total_tokens ?? 0) + response.token_usage.total_tokens;
        const currentTaskUsage = current?.by_question?.[questionId] ?? { total_interactions: 0, total_tokens: 0 };
        return {
          total_interactions: totalInteractions,
          total_tokens: totalTokens,
          average_tokens_per_interaction: Math.floor(totalTokens / totalInteractions),
          by_question: {
            ...(current?.by_question ?? {}),
            [questionId]: {
              total_interactions: currentTaskUsage.total_interactions + 1,
              total_tokens: currentTaskUsage.total_tokens + response.token_usage.total_tokens,
              token_efficiency: currentTaskUsage.token_efficiency
            }
          }
        };
      });
      void getAiUsage(assessment.assessment_id).then((usage) => {
        setAiUsageSummary({
          total_interactions: usage.total_interactions,
          total_tokens: usage.total_tokens,
          average_tokens_per_interaction: usage.average_tokens_per_interaction,
          by_question: usage.by_question ?? {}
        });
      }).catch(() => undefined);
      const suggestion = response.suggestion;
      const workspaceActions = response.workspace_actions?.length
        ? response.workspace_actions
        : suggestion
          ? [{
            type: "replace_file" as const,
            label: suggestion.apply_label,
            target_file: suggestion.target_file,
            language: suggestion.language,
            replacement_code: suggestion.replacement_code
          }]
          : [];
      setAiMessagesByQuestion((current) => ({
        ...current,
        [questionId]: (current[questionId] ?? []).map((item) =>
          item.clientId === pendingAssistantId
            ? {
              clientId: pendingAssistantId,
              role: "assistant",
              interactionId: response.interaction_id,
              responseVisibleAt: performance.now(),
              text: response.response_markdown,
              suggestedCode: suggestion?.replacement_code ?? undefined,
              suggestedLanguage: suggestion ? suggestion.language : undefined,
              targetFile: suggestion?.target_file ?? undefined,
              applyLabel: suggestion?.apply_label ?? undefined,
              workspaceActions,
              tokenUsage: response.token_usage
            }
            : item
        )
      }));
      void recordAiInteractionEvent({
        assessment_id: assessment.assessment_id,
        interaction_id: response.interaction_id,
        event_type: "response_visible"
      }).catch(() => undefined);
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "AI request failed.";
      setAiMessagesByQuestion((current) => ({
        ...current,
        [questionId]: (current[questionId] ?? []).map((item) =>
          item.clientId === pendingAssistantId
            ? {
              clientId: pendingAssistantId,
              role: "assistant",
              status: "failed",
              text: `AI request failed: ${message}`
            }
            : item
        )
      }));
      setError(message);
    } finally {
      setAiState("idle");
    }
  }

  function buildQuestionStateWithAiReplacements(actions: AiWorkspaceAction[]) {
    if (actions.length === 0) {
      setError("AI action did not include file edits.");
      return null;
    }

    const unsupportedAction = actions.find((action) => !isAiActionLanguageAllowed(activeQuestion, language, action.target_file, action.language));
    if (unsupportedAction) {
      setError("AI action language is not allowed for this task.");
      return null;
    }

    const targetLanguage = resolveAllowedLanguage(activeQuestion, actions[0].language ?? language);
    const mismatchedLanguage = actions.find((action) => resolveAllowedLanguage(activeQuestion, action.language ?? targetLanguage) !== targetLanguage);
    if (mismatchedLanguage) {
      setError("AI action files must use the same selected task language.");
      return null;
    }

    const nextQuestionStates = persistCurrentCode();
    const currentQuestionState = sanitizeQuestionState(
      activeQuestion,
      nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, targetLanguage),
      targetLanguage
    );
    const visibleFiles = new Set([
      ...Object.keys(getStarterFiles(activeQuestion, targetLanguage)),
      ...Object.entries(currentQuestionState.files)
        .filter(([, file]) => file.language === targetLanguage)
        .map(([fileName]) => fileName)
    ]);

    let nextFiles = currentQuestionState.files;
    let firstTargetFile = currentQuestionState.active_file;
    let firstReplacementCode = getCodeFromState(currentQuestionState, activeQuestion, targetLanguage, firstTargetFile);

    for (const action of actions) {
      const replacementCode = action.replacement_code;
      if (!replacementCode) {
        setError("AI action did not include replacement code.");
        return null;
      }

      const targetFile = action.target_file ?? currentQuestionState.active_file;
      if (!visibleFiles.has(targetFile)) {
        setError("AI action target file is not visible in this task.");
        return null;
      }

      if (nextFiles === currentQuestionState.files) {
        firstTargetFile = targetFile;
        firstReplacementCode = replacementCode;
      }

      nextFiles = {
        ...nextFiles,
        [targetFile]: {
          language: targetLanguage,
          content: replacementCode
        }
      };
    }

    const updatedQuestionState: WorkspaceQuestionState = {
      ...currentQuestionState,
      selected_language: targetLanguage,
      active_file: firstTargetFile,
      files: nextFiles
    };

    return {
      nextQuestionStates,
      updatedQuestionState,
      targetFile: firstTargetFile,
      targetLanguage,
      replacementCode: firstReplacementCode
    };
  }

  async function executeAiWorkspaceActions(message: AiChatMessage, runAfterApply: boolean, eventTimestamp: number) {
    if (workspaceFrozen || agentActionState === "applying") {
      return;
    }

    const replaceActions = getMessageReplaceFileActions(message, activeFile, language);
    const shouldRun = runAfterApply || (replaceActions.length === 0 && messageHasRunPublicChecksAction(message));
    setError(null);

    if (replaceActions.length === 0) {
      if (shouldRun) {
        const selectedLanguage = resolveAllowedLanguage(activeQuestion, language);
        const currentQuestionState = sanitizeQuestionState(activeQuestion, persistCurrentCode()[activeQuestionId], selectedLanguage);
        setOutputTab("console");
        await runPublicChecksForState(currentQuestionState);
      }
      return;
    }

    const replacement = buildQuestionStateWithAiReplacements(replaceActions);
    if (!replacement) {
      return;
    }

    const { nextQuestionStates, updatedQuestionState, targetFile, targetLanguage, replacementCode } = replacement;
    if (message.interactionId) {
      appliedSuggestionRef.current = {
        interactionId: message.interactionId,
        code: replacementCode,
        appliedAt: eventTimestamp
      };
      void recordAiInteractionEvent({
        assessment_id: assessment.assessment_id,
        interaction_id: message.interactionId,
        event_type: "apply",
        elapsed_milliseconds: Math.max(0, Math.round(eventTimestamp - (message.responseVisibleAt ?? eventTimestamp))),
        applied_unchanged: true,
        metadata: { run_after_apply: String(runAfterApply) }
      }).catch(() => undefined);
    }

    const updatedStates = {
      ...nextQuestionStates,
      [activeQuestionId]: updatedQuestionState
    };

    setAgentActionState("applying");
    setQuestionStates(updatedStates);
    setLanguage(targetLanguage);
    setActiveFile(targetFile);
    setCode(replacementCode);
    setOutputTab(runAfterApply ? "console" : "preview");
    setSaveState("saving");

    try {
      const savedWorkspace = await saveWorkspace(assessment.assessment_id, { [activeQuestionId]: updatedQuestionState });
      setQuestionStates((current) => normalizeWorkspaceQuestionStates(
        assessment.questions,
        mergeQuestionStates(current, savedWorkspace.questions)
      ));
      setSaveState("saved");

      if (runAfterApply) {
        await runPublicChecksForState(updatedQuestionState);
      }
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "AI action failed.");
      setSaveState("unsaved");
    } finally {
      setAgentActionState("idle");
    }
  }

  function rejectAiSuggestion(message: AiChatMessage, eventTimestamp: number) {
    if (message.interactionId) {
      void recordAiInteractionEvent({
        assessment_id: assessment.assessment_id,
        interaction_id: message.interactionId,
        event_type: "reject",
        elapsed_milliseconds: Math.max(0, Math.round(eventTimestamp - (message.responseVisibleAt ?? eventTimestamp)))
      }).catch(() => undefined);
    }
    setAiMessagesByQuestion((current) => ({
      ...current,
      [activeQuestionId]: (current[activeQuestionId] ?? []).map((item) =>
        item === message ? { ...item, workspaceActions: [], suggestedCode: undefined } : item
      )
    }));
  }

  async function downloadAiTranscript() {
    try {
      const transcript = await getAiTaskTranscript(assessment.assessment_id, activeQuestionId);
      const file = new Blob([JSON.stringify(transcript, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(file);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `task-${activeQuestionIndex + 1}-ai-transcript.json`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Could not download the AI transcript.");
    }
  }

  async function submitFinal() {
    if (isSubmitPending) {
      return;
    }

    if (!sandboxAvailable) {
      setConfirmSubmit(false);
      setError(SANDBOX_UNAVAILABLE_MESSAGE);
      return;
    }

    setConfirmSubmit(false);
    setWorkspaceFrozen(true);
    setFrozenTimeLabel(remainingTime);
    setError(null);
    setSaveState("saving");
    setSubmitState("saving");
    try {
      const nextQuestionStates = normalizeWorkspaceQuestionStates(assessment.questions, persistCurrentCode());
      setQuestionStates(nextQuestionStates);
      const savedWorkspace = await saveWorkspace(assessment.assessment_id, nextQuestionStates);
      setQuestionStates((current) => normalizeWorkspaceQuestionStates(
        assessment.questions,
        mergeQuestionStates(current, savedWorkspace.questions)
      ));
      setSaveState("saved");
      setSubmitState("submitting");
      const submission = await finalizeSubmission(assessment.assessment_id);
      router.push(submission.reflection_required
        ? `/student/assessments/${assessment.assessment_id}/reflection`
        : `/student/assessments/${assessment.assessment_id}/review?submissionId=${submission.submission_id}`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Submission failed.");
      setSaveState("unsaved");
      setSubmitState("idle");
      setWorkspaceFrozen(false);
      setFrozenTimeLabel(null);
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
              className="grid h-9 w-9 place-items-center rounded-xl border border-cyanGlow/35 bg-cyanGlow/10 text-cyanGlow transition hover:border-cyanGlow/70 hover:bg-cyanGlow/15"
              title="Show tasks panel"
              aria-label="Show tasks panel"
              onClick={ideLayout.toggleTasksCollapsed}
            >
              <PanelLeftOpen size={18} />
            </button>
            <span className="mt-3 text-[10px] uppercase tracking-[0.14em] text-white/55 [writing-mode:vertical-rl]">
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
              className="flex h-8 shrink-0 items-center gap-1.5 rounded-lg border border-white/15 bg-[#0e1726] px-2.5 text-xs text-white/70 transition hover:border-cyanGlow/50 hover:text-cyanGlow"
              title="Hide tasks panel"
              aria-label="Hide tasks panel"
              onClick={ideLayout.toggleTasksCollapsed}
            >
              <PanelLeftClose size={15} />
              <span className="hidden sm:inline">Hide</span>
            </button>
          </div>
          <span className="badge mt-3 hidden w-fit shrink-0 xl:inline-flex">Active attempt</span>
        </div>
        <div className="relative mt-3 shrink-0 rounded-[16px] border border-cyanGlow/25 bg-gradient-to-br from-cyanGlow/10 to-purpleGlow/[0.06] p-3">
          <div className="flex items-center justify-between gap-3">
            <button
              type="button"
              className="grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-white/15 bg-black/20 text-white/70 transition hover:border-cyanGlow/50 hover:text-cyanGlow disabled:cursor-not-allowed disabled:opacity-30"
              disabled={workspaceFrozen || activeQuestionIndex === 0}
              onClick={() => moveQuestion(-1)}
              aria-label="Previous question"
              title="Previous question"
            >
              <ChevronLeft size={17} />
            </button>
            <div className="min-w-0 text-center">
              <p className="text-[10px] uppercase tracking-[0.16em] text-cyanGlow/70">
                Question {activeQuestionIndex + 1} of {assessment.questions.length}
              </p>
              <p className="mt-1 truncate text-sm font-semibold text-white">{activeQuestion?.title}</p>
            </div>
            <button
              type="button"
              className="grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-white/15 bg-black/20 text-white/70 transition hover:border-cyanGlow/50 hover:text-cyanGlow disabled:cursor-not-allowed disabled:opacity-30"
              disabled={workspaceFrozen || activeQuestionIndex === assessment.questions.length - 1}
              onClick={() => moveQuestion(1)}
              aria-label="Next question"
              title="Next question"
            >
              <ChevronRight size={17} />
            </button>
          </div>
          <div className="mt-3 flex justify-center gap-1.5" aria-label="Question navigation">
            {assessment.questions.map((question, index) => (
              <button
                key={question.question_id}
                type="button"
                disabled={workspaceFrozen}
                onClick={() => switchQuestion(question)}
                className={`h-2 rounded-full transition-all ${
                  index === activeQuestionIndex
                    ? "w-7 bg-cyanGlow shadow-[0_0_10px_rgba(0,229,255,0.45)]"
                    : "w-2 bg-white/20 hover:bg-white/45"
                }`}
                aria-label={`Open question ${index + 1}: ${question.title}`}
                aria-current={index === activeQuestionIndex ? "step" : undefined}
              />
            ))}
          </div>
        </div>
        <div className="scrollbar-soft scanline relative mt-3 min-h-0 flex-1 overflow-y-auto rounded-[16px] border border-white/10 bg-black/20 p-4 leading-relaxed">
          <p className="text-[11px] uppercase tracking-[0.16em] text-cyanGlow/70">Problem statement</p>
          <div className="mt-3 flex flex-wrap gap-2">
            <span className="badge"><SemanticIcon name={activeTaskIcon} size={13} /> {formatTaskType(activeQuestion?.task_type)}</span>
            <span className="badge">Difficulty: {formatDifficulty(activeQuestion?.difficulty)}</span>
            <span className="badge">Run: {formatVerificationMode(activeQuestion?.verification_mode)}</span>
          </div>
          <div className="mt-4 space-y-3 text-sm text-white/80 [&_p]:leading-relaxed [&_li]:leading-relaxed [&_code]:rounded [&_code]:bg-white/8 [&_code]:px-1 [&_code]:py-0.5">
            {renderMarkdown(limitProblemStatement(activeQuestion?.problem_description_markdown ?? ""))}
          </div>
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
          <span className="badge hidden xl:inline-flex"><SemanticIcon name="clock" size={14} /> {frozenTimeLabel ?? remainingTime}</span>
          <span className="badge">{saveState}</span>
          <button className="btn-primary px-4 py-2 disabled:cursor-not-allowed disabled:opacity-45" onClick={() => setConfirmSubmit(true)} disabled={isSubmitPending || !sandboxAvailable}>
            {isSubmitPending ? <Loader2 className="animate-spin" size={16} /> : null}
            {!sandboxAvailable ? "Submit unavailable" : submitState === "saving" ? "Saving..." : submitState === "submitting" ? "Submitting..." : "Submit"}
          </button>
        </div>
        {!sandboxAvailable ? <p className="relative border-b border-amber-500/20 bg-[#241d0d] px-4 py-2 text-sm text-amber-100/80">{SANDBOX_UNAVAILABLE_MESSAGE}</p> : null}
        {error ? <p className="relative border-b border-white/10 px-4 py-2 text-sm text-pinkGlow">{error}</p> : null}
        {isSubmitPending ? (
          <p className="relative border-b border-white/10 px-4 py-2 text-sm text-white/55" aria-live="polite">
            {submitState === "saving"
              ? "Saving current workspace before final submission..."
              : "Submitting your solution for final evaluation. You will continue to review when it is complete."}
          </p>
        ) : null}
        <div className="relative grid gap-2 border-b border-white/10 px-3 py-2.5">
          <div className="flex min-w-0 items-center gap-2">
            <span className="flex shrink-0 items-center gap-1.5 pr-1 text-xs text-white/35">
              <SemanticIcon name="folder" size={14} /> Files
            </span>
            <div className="scrollbar-soft flex min-w-0 flex-1 gap-1 overflow-x-auto pb-1">
              {fileNames.map((fileName) => (
                <button
                  key={fileName}
                  onClick={() => switchFile(fileName)}
                  disabled={workspaceFrozen}
                  className={`flex shrink-0 items-center gap-1.5 rounded-lg border px-3 py-1.5 font-mono text-xs transition ${
                    fileName === activeFile
                      ? "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow shadow-[0_0_18px_rgba(0,229,255,0.06)]"
                      : "border-white/10 bg-white/5 text-white/45 hover:bg-white/10 hover:text-white/70"
                  }`}
                  title={fileName}
                >
                  <SemanticIcon name="file" size={13} className="shrink-0" />
                  <span className="max-w-40 truncate">{fileName}</span>
                </button>
              ))}
            </div>
          </div>
          <div className="flex min-w-0 items-center gap-2">
            <span className="shrink-0 text-xs text-white/40">Language</span>
            <CustomDropdown
              ariaLabel="Language"
              className="min-w-0 flex-1"
              value={language}
              disabled={workspaceFrozen}
              options={STUDENT_LANGUAGE_OPTIONS.filter((option) => visibleLanguages.includes(option.value))}
              onChange={switchLanguage}
            />
            <button className="btn-secondary shrink-0 px-4 py-2" onClick={handleRun} disabled={workspaceFrozen || runState === "running" || !sandboxAvailable}>
              <SemanticIcon name="play" size={16} />
              {!sandboxAvailable ? "Unavailable" : isRunningActiveTask ? "Running..." : "Run"}
            </button>
          </div>
        </div>
        <div className="relative min-h-0 flex-1 overflow-hidden bg-black/30 p-3">
          <MonacoCodeEditor
            assessmentId={assessment.assessment_id}
            questionId={activeQuestionId}
            fileName={activeFile}
            language={language}
            value={code}
            onChange={updateCode}
            readOnly={workspaceFrozen}
          />
        </div>
      </section>

      <button
        type="button"
        className="workspace-resizer workspace-resizer-vertical workspace-resizer-agent"
        aria-label="Resize AI panel"
        title="Drag left or right to resize the AI panel"
        disabled={ideLayout.isAgentCollapsed}
        onPointerDown={ideLayout.startResize("agent")}
        style={{ gridColumn: "4", gridRow: "1" }}
      >
        <span className="sr-only">Drag left or right to resize the AI panel</span>
      </button>

      <aside
        className={`panel dynamic-surface flex min-h-0 min-w-0 flex-col rounded-xl ${ideLayout.isAgentCollapsed ? "items-center p-2" : "p-3"}`}
        style={{ gridColumn: "5", gridRow: "1" }}
      >
        {ideLayout.isAgentCollapsed ? (
          <>
            <button
              type="button"
              className="grid h-9 w-9 place-items-center rounded-xl border border-cyanGlow/35 bg-cyanGlow/10 text-cyanGlow transition hover:border-cyanGlow/70 hover:bg-cyanGlow/15"
              title="Show AI panel"
              aria-label="Show AI panel"
              onClick={ideLayout.toggleAgentCollapsed}
            >
              <PanelRightOpen size={18} />
            </button>
            <span className="mt-3 text-[10px] uppercase tracking-[0.14em] text-white/55 [writing-mode:vertical-rl]">
              AI Agent
            </span>
          </>
        ) : (
          <>
        <div className="relative grid gap-3">
          <div className="flex min-w-0 items-start gap-3">
            <span className="float-soft grid h-10 w-10 shrink-0 place-items-center rounded-2xl border border-cyanGlow/20 bg-[linear-gradient(145deg,rgba(0,229,255,0.14),rgba(168,85,247,0.16))] text-cyanGlow">
              <SemanticIcon name="ai" size={22} />
            </span>
            <div className="min-w-0 flex-1">
              <h2 className="font-semibold">AI Agent</h2>
              <p className="mt-0.5 truncate text-xs text-white/40">
                {assessment.ai_enabled ? `Task ${activeQuestionIndex + 1}: ${activeQuestion.title}` : "Disabled for this assessment"}
              </p>
            </div>
            <button
              type="button"
              className="flex h-8 shrink-0 items-center gap-1.5 rounded-lg border border-white/15 bg-[#0e1726] px-2.5 text-xs text-white/70 transition hover:border-cyanGlow/50 hover:text-cyanGlow"
              title="Hide AI panel"
              aria-label="Hide AI panel"
              onClick={ideLayout.toggleAgentCollapsed}
            >
              <PanelRightClose size={15} />
              <span>Hide</span>
            </button>
          </div>
          <div className="grid grid-cols-2 gap-1 rounded-xl border border-white/10 bg-black/20 p-1">
            {([
              { page: "chat" as const, label: "Chat", icon: "ai" as const },
              { page: "usage" as const, label: "Usage", icon: "results" as const }
            ]).map(({ page, label, icon }) => (
              <button
                key={page}
                type="button"
                className={`flex items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition ${
                  aiAgentPage === page
                    ? "bg-cyanGlow/15 text-cyanGlow"
                    : "text-white/45 hover:bg-white/5 hover:text-white/70"
                }`}
                aria-pressed={aiAgentPage === page}
                onClick={() => setAiAgentPage(page)}
              >
                <SemanticIcon name={icon} size={13} />
                {label}
              </button>
            ))}
          </div>
          {aiAgentPage === "usage" && assessment.ai_enabled ? (
            <div className="flex w-full items-center justify-between gap-3 rounded-xl border border-cyanGlow/25 bg-cyanGlow/10 px-3 py-2">
              <p className="text-[10px] uppercase tracking-[0.14em] text-white/40">This task&apos;s AI usage</p>
              <p className="truncate text-right text-xs text-white/50">
                <span className="font-mono font-semibold text-cyanGlow">
                  {formatTokenCount(activeTaskAiUsage.total_tokens)}
                </span>
                {" tokens · "}
                {activeTaskAiUsage.total_interactions} interactions
              </p>
            </div>
          ) : null}
          {aiAgentPage === "usage" && assessment.ai_enabled ? (
            <div className="flex w-full items-center justify-between gap-3 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2">
              <p className="text-[10px] uppercase tracking-[0.14em] text-white/40">Assessment total</p>
              <p className="truncate text-right text-xs text-white/50">
                <span className="font-mono font-semibold text-white/85">
                  {formatTokenCount(aiUsageSummary?.total_tokens ?? 0)}
                </span>
                {" tokens - "}
                {aiUsageSummary?.total_interactions ?? 0} interactions
              </p>
            </div>
          ) : null}
          {aiAgentPage === "usage" && !assessment.ai_enabled ? (
            <div className="rounded-xl border border-white/10 bg-white/[0.03] p-3 text-sm text-white/45">
              AI disabled for this assessment
            </div>
          ) : null}
          {aiAgentPage === "usage" && assessment.ai_enabled && activeTaskAiUsage.token_efficiency ? (
            <div className="grid grid-cols-2 gap-2 rounded-xl border border-white/10 bg-black/20 p-2 text-[10px] text-white/45 sm:grid-cols-3">
              <div>
                <p className="uppercase tracking-[0.1em]">Prompt CpT</p>
                <p className="mt-0.5 font-mono text-xs text-white/80">{activeTaskAiUsage.token_efficiency.prompt_source.characters_per_token.toFixed(2)}</p>
              </div>
              <div>
                <p className="uppercase tracking-[0.1em]">Prompt TpC</p>
                <p className="mt-0.5 font-mono text-xs text-white/80">{activeTaskAiUsage.token_efficiency.prompt_source.tokens_per_character.toFixed(3)}</p>
              </div>
              <div>
                <p className="uppercase tracking-[0.1em]">Context</p>
                <p className="mt-0.5 font-mono text-xs text-white/80">
                  {activeTaskAiUsage.token_efficiency.context_signals_provided}/{activeTaskAiUsage.token_efficiency.required_context_signals}
                </p>
              </div>
              <div>
                <p className="uppercase tracking-[0.1em]">Response CpT</p>
                <p className="mt-0.5 font-mono text-xs text-white/80">{activeTaskAiUsage.token_efficiency.response.characters_per_token.toFixed(2)}</p>
              </div>
              <div>
                <p className="uppercase tracking-[0.1em]">Response TpC</p>
                <p className="mt-0.5 font-mono text-xs text-white/80">{activeTaskAiUsage.token_efficiency.response.tokens_per_character.toFixed(3)}</p>
              </div>
              <p className="col-span-2 leading-4 text-white/30 sm:col-span-3">These metrics inform AI Usage scoring only after this task fully passes.</p>
            </div>
          ) : null}
          {aiAgentPage === "usage" && assessment.ai_enabled ? (
            <button
              type="button"
              className="w-fit text-xs text-cyanGlow/80 underline-offset-4 hover:text-cyanGlow hover:underline"
              onClick={() => void downloadAiTranscript()}
            >
              Download this task&apos;s AI transcript
            </button>
          ) : null}
        </div>
        {aiAgentPage === "chat" ? (
          <>
        <div className="relative mt-4 grid grid-cols-1 gap-2 xl:grid-cols-2">
          {(["code_suggestion", "explanation", "debugging"] as AiInteractionType[]).map((type) => (
            <button
              key={type}
              disabled={workspaceFrozen || !assessment.ai_enabled || aiState === "running"}
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
                  Response in progress
                </span>
              ) : null}
              {message.status === "failed" ? (
                <span className="mb-2 inline-flex items-center gap-2 text-xs text-pinkGlow">
                  Response unavailable
                </span>
              ) : null}
              {renderMarkdown(message.text)}
              {message.role === "assistant" ? (
                <AiMessageActionButtons
                  message={message}
                  activeFile={activeFile}
                  language={language}
                  isApplying={workspaceFrozen || agentActionState === "applying"}
                  isRunning={runState === "running"}
                  canRun={sandboxAvailable}
                  onExecute={(runAfterApply, eventTimestamp) => void executeAiWorkspaceActions(message, runAfterApply, eventTimestamp)}
                  onReject={(eventTimestamp) => rejectAiSuggestion(message, eventTimestamp)}
                />
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
            disabled={workspaceFrozen || !assessment.ai_enabled || aiState === "running"}
            onChange={(event) => setAiDraftsByQuestion((current) => ({ ...current, [activeQuestionId]: event.target.value }))}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void sendAi("code_suggestion");
              }
            }}
          />
          <button
            className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-cyanGlow text-slate-950 disabled:cursor-not-allowed disabled:opacity-45"
            disabled={workspaceFrozen || !assessment.ai_enabled || aiState === "running"}
            onClick={() => sendAi("code_suggestion")}
            aria-label="Send AI request"
          >
            <SemanticIcon name="send" size={16} />
          </button>
        </div>
          </>
        ) : null}
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
            className={`ml-2 flex items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs transition ${
              ideLayout.isOutputCollapsed
                ? "border-cyanGlow/35 bg-cyanGlow/10 text-cyanGlow hover:border-cyanGlow/70"
                : "border-white/15 bg-white/5 text-white/70 hover:border-cyanGlow/50 hover:text-cyanGlow"
            }`}
            title={ideLayout.isOutputCollapsed ? "Show output" : "Hide output"}
            aria-label={ideLayout.isOutputCollapsed ? "Show output panel" : "Hide output panel"}
            type="button"
            onClick={ideLayout.toggleOutputCollapsed}
          >
            {ideLayout.isOutputCollapsed ? <PanelBottomOpen size={15} /> : <PanelBottomClose size={15} />}
            <span className="hidden sm:inline">{ideLayout.isOutputCollapsed ? "Show" : "Hide"}</span>
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
            <button className="btn-primary" onClick={submitFinal} disabled={isSubmitPending || !sandboxAvailable}>
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
