"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { Brain, Clock, Play, Send, Sparkles, UploadCloud, X } from "lucide-react";
import { autosaveWorkspace, finalizeSubmission, getAiResponse, getAiState, runCode, saveWorkspace } from "@/lib/api";
import { MonacoCodeEditor } from "@/components/workspace/MonacoCodeEditor";
import type { AiHintLevel, AiState, Assessment, Language, Question, RunResult, WorkspaceQuestionState, WorkspaceState } from "@/lib/types";

interface WorkspaceClientProps {
  assessment: Assessment;
  workspace: WorkspaceState;
}

type SaveState = "saved" | "unsaved" | "saving";

const STUDENT_LANGUAGES: Array<{ value: Language; label: string }> = [
  { value: "python", label: "Python" },
  { value: "javascript", label: "JavaScript" },
  { value: "typescript", label: "TypeScript" }
];

const HINT_LEVELS: Array<{ value: AiHintLevel; label: string; fallbackCost: number }> = [
  { value: "concept_hint", label: "Concept", fallbackCost: 1 },
  { value: "strategy_hint", label: "Strategy", fallbackCost: 2 },
  { value: "debugging_hint", label: "Debugging", fallbackCost: 3 },
  { value: "pseudocode_hint", label: "Pseudocode", fallbackCost: 4 },
  { value: "code_level_suggestion", label: "Code-level", fallbackCost: 6 }
];

function getFileNameForLanguage(language: Language) {
  if (language === "javascript") return "main.js";
  if (language === "typescript") return "main.ts";
  return "main.py";
}

function createQuestionState(question: Question | undefined, language: Language = "python"): WorkspaceQuestionState {
  const fileName = getFileNameForLanguage(language);

  return {
    selected_language: language,
    active_file: fileName,
    files: {
      [fileName]: {
        language,
        content: question?.starter_code[language] ?? ""
      }
    },
    last_saved_at: "",
    version: 0,
    ai_credits_remaining: question?.ai_credit_budget ?? null
  };
}

function getCodeFromState(state: WorkspaceQuestionState | undefined, question: Question | undefined, language: Language) {
  const fileName = getFileNameForLanguage(language);
  return state?.files[fileName]?.content ?? question?.starter_code[language] ?? "";
}

function buildRunFailureSummary(runResult: RunResult | null, error: string | null) {
  if (error) {
    return error;
  }

  if (!runResult) {
    return null;
  }

  if (runResult.status === "runtime_error") {
    return runResult.stderr ?? runResult.stdout ?? "Runtime error occurred during execution.";
  }

  if (runResult.status === "failed") {
    const failingTests = runResult.test_results.filter((test) => !test.passed);
    const failingSummary = failingTests.length
      ? failingTests.map((test) => `${test.name}${test.output ? `: ${test.output}` : ""}`).join("; ")
      : "The sample tests failed.";

    return `${failingSummary}\n\nStdout: ${runResult.stdout || "(empty)"}${runResult.stderr ? `\nStderr: ${runResult.stderr}` : ""}`;
  }

  return null;
}

function buildDebugPrompt(runResult: RunResult | null, error: string | null) {
  const summary = buildRunFailureSummary(runResult, error);
  if (!summary) {
    return "Please help me debug my current solution.";
  }

  return [
    "I ran my current solution and need debugging help.",
    "",
    "Issue summary:",
    summary,
    "",
    "Please point out the most likely cause and suggest a next step without giving away the full final answer."
  ].join("\n");
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

function mergeAiCreditsIntoQuestionStates(current: WorkspaceState["questions"], aiState: AiState) {
  return Object.entries(aiState.questions).reduce<WorkspaceState["questions"]>((nextStates, [questionId, creditState]) => {
    const existing = nextStates[questionId];
    if (!existing) return nextStates;

    return {
      ...nextStates,
      [questionId]: {
        ...existing,
        ai_credits_remaining: creditState.ai_credits_remaining
      }
    };
  }, current);
}

function updateAiStateCredits(current: AiState, questionId: string, creditsRemaining: number | null) {
  return {
    ...current,
    questions: {
      ...current.questions,
      [questionId]: {
        ...current.questions[questionId],
        ai_credits_remaining: creditsRemaining
      }
    }
  };
}

function getHintCost(aiState: AiState | null, level: AiHintLevel) {
  return aiState?.hint_levels.find((hint) => hint.hint_level === level)?.credit_cost
    ?? HINT_LEVELS.find((hint) => hint.value === level)?.fallbackCost
    ?? 1;
}

function getHintLabel(level: AiHintLevel) {
  return HINT_LEVELS.find((hint) => hint.value === level)?.label ?? "Structured";
}

function parseInlineCode(text: string) {
  const parts = text.split("`");
  return parts.map((part, index) => {
    if (index % 2 === 1) {
      return (
        <code key={index} className="rounded bg-white/10 px-1.5 py-0.5 font-mono text-[11px] text-cyanGlow">
          {part}
        </code>
      );
    }
    return part;
  });
}

function renderMarkdown(text: string) {
  if (!text) return null;

  const elements: React.ReactNode[] = [];
  const lines = text.split("\n");
  let inCodeBlock = false;
  let codeBlockLines: string[] = [];
  let codeBlockLang = "";

  const parseBoldAndInlineCode = (str: string): React.ReactNode => {
    const boldParts = str.split("**");
    return boldParts.map((boldPart, bIdx) => {
      const isBold = bIdx % 2 === 1;
      const codeParts = boldPart.split("`");
      const renderedCodeParts = codeParts.map((codePart, cIdx) => {
        if (cIdx % 2 === 1) {
          return (
            <code key={`code-${bIdx}-${cIdx}`} className="rounded bg-white/10 px-1.5 py-0.5 font-mono text-[11px] text-cyanGlow">
              {codePart}
            </code>
          );
        }
        return codePart;
      });

      if (isBold) {
        return <strong key={`bold-${bIdx}`} className="font-semibold text-white">{renderedCodeParts}</strong>;
      }
      return <span key={`text-${bIdx}`}>{renderedCodeParts}</span>;
    });
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();

    if (trimmed.startsWith("```")) {
      if (inCodeBlock) {
        const codeContent = codeBlockLines.join("\n");
        elements.push(
          <div key={`code-${i}`} className="my-3 overflow-hidden rounded-xl border border-white/10 bg-black/40 font-mono text-xs">
            {codeBlockLang && (
              <div className="flex items-center justify-between border-b border-white/5 bg-white/5 px-3 py-1.5 text-[10px] uppercase tracking-wider text-white/40">
                <span>{codeBlockLang}</span>
              </div>
            )}
            <pre className="scrollbar-soft overflow-x-auto p-3 text-cyanGlow/90 whitespace-pre">
              {codeContent}
            </pre>
          </div>
        );
        inCodeBlock = false;
        codeBlockLines = [];
        codeBlockLang = "";
      } else {
        inCodeBlock = true;
        codeBlockLang = trimmed.slice(3).trim();
      }
      continue;
    }

    if (inCodeBlock) {
      codeBlockLines.push(line);
      continue;
    }

    if (trimmed.startsWith("### ")) {
      elements.push(
        <h4 key={i} className="mt-4 text-xs font-semibold uppercase tracking-wider text-cyanGlow/90">
          {parseBoldAndInlineCode(trimmed.slice(4))}
        </h4>
      );
    } else if (trimmed.startsWith("## ")) {
      elements.push(
        <h3 key={i} className="mt-5 text-sm font-bold uppercase tracking-wider text-white">
          {parseBoldAndInlineCode(trimmed.slice(3))}
        </h3>
      );
    } else if (trimmed.startsWith("# ")) {
      elements.push(
        <h2 key={i} className="mt-6 text-base font-extrabold text-white">
          {parseBoldAndInlineCode(trimmed.slice(2))}
        </h2>
      );
    } else if (trimmed.startsWith("- ") || trimmed.startsWith("* ")) {
      elements.push(
        <div key={i} className="ml-2 mt-1 flex items-start gap-2 text-sm text-white/70">
          <span className="text-cyanGlow select-none mt-0.5">•</span>
          <span>{parseBoldAndInlineCode(trimmed.slice(2))}</span>
        </div>
      );
    } else if (trimmed.startsWith("> ")) {
      elements.push(
        <blockquote key={i} className="my-2 border-l-2 border-cyanGlow/40 bg-cyanGlow/5 py-1.5 pl-3 pr-2 text-xs italic leading-5 text-white/60 rounded-r-lg">
          {parseBoldAndInlineCode(trimmed.slice(2))}
        </blockquote>
      );
    } else if (trimmed === "") {
      elements.push(<div key={i} className="h-2" />);
    } else {
      elements.push(
        <p key={i} className="mt-1 text-sm leading-6 text-white/65">
          {parseBoldAndInlineCode(line)}
        </p>
      );
    }
  }

  if (inCodeBlock && codeBlockLines.length > 0) {
    elements.push(
      <div key="code-unclosed" className="my-3 overflow-hidden rounded-xl border border-white/10 bg-black/40 font-mono text-xs">
        <pre className="scrollbar-soft overflow-x-auto p-3 text-cyanGlow/90 whitespace-pre">
          {codeBlockLines.join("\n")}
        </pre>
      </div>
    );
  }

  return elements;
}

export function WorkspaceClient({ assessment, workspace }: WorkspaceClientProps) {
  const router = useRouter();
  const firstQuestion = assessment.questions[0];
  const [activeQuestionId, setActiveQuestionId] = useState(firstQuestion?.question_id ?? "q-two-sum");
  const [questionStates, setQuestionStates] = useState(workspace.questions);
  const activeQuestion = useMemo(
    () => assessment.questions.find((question) => question.question_id === activeQuestionId) ?? assessment.questions[0],
    [assessment.questions, activeQuestionId]
  );
  const initialState = questionStates[activeQuestionId] ?? Object.values(questionStates)[0] ?? createQuestionState(activeQuestion);
  const [language, setLanguage] = useState<Language>(initialState?.selected_language ?? "python");
  const [code, setCode] = useState(getCodeFromState(initialState, activeQuestion, initialState?.selected_language ?? "python"));
  const [saveState, setSaveState] = useState<SaveState>("saved");
  const [runState, setRunState] = useState<"idle" | "running">("idle");
  const [runResult, setRunResult] = useState<RunResult | null>(null);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [aiState, setAiState] = useState<AiState | null>(null);
  const [selectedHintLevel, setSelectedHintLevel] = useState<AiHintLevel>("concept_hint");
  const [aiMessage, setAiMessage] = useState("");
  const [messages, setMessages] = useState([
    { role: "assistant", text: "Choose a structured hint level. Each hint spends credits from the active question only." }
  ]);

  useEffect(() => {
    if (!assessment.ai_enabled) {
      setAiState(null);
      return;
    }

    let cancelled = false;
    getAiState(assessment.assessment_id)
      .then((nextAiState) => {
        if (cancelled) return;
        setAiState(nextAiState);
        setQuestionStates((current) => mergeAiCreditsIntoQuestionStates(current, nextAiState));
      })
      .catch((exception) => {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : "AI state failed to load.");
        }
      });

    return () => {
      cancelled = true;
    };
  }, [assessment.ai_enabled, assessment.assessment_id]);

  useEffect(() => {
    setSaveState("unsaved");
    const saving = window.setTimeout(() => setSaveState("saving"), 450);
    const saved = window.setTimeout(async () => {
      try {
        const activeFile = getFileNameForLanguage(language);
        const savedWorkspace = await autosaveWorkspace(
          assessment.assessment_id,
          activeQuestionId,
          language,
          activeFile,
          code
        );
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
  }, [activeQuestionId, assessment.assessment_id, code, language]);

  function persistCurrentCode(nextQuestionStates = questionStates) {
    const activeFile = getFileNameForLanguage(language);
    return {
      ...nextQuestionStates,
      [activeQuestionId]: {
        ...(nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, language)),
        selected_language: language,
        active_file: activeFile,
        files: {
          ...(nextQuestionStates[activeQuestionId]?.files ?? {}),
          [activeFile]: { language, content: code }
        },
        last_saved_at: nextQuestionStates[activeQuestionId]?.last_saved_at ?? new Date().toISOString(),
        version: nextQuestionStates[activeQuestionId]?.version ?? 0
      }
    };
  }

  function updateCode(nextCode: string) {
    setCode(nextCode);
    setQuestionStates((current) => {
      const activeFile = getFileNameForLanguage(language);
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

  function switchLanguage(nextLanguage: Language) {
    const nextQuestionStates = persistCurrentCode();
    const currentQuestionState = nextQuestionStates[activeQuestionId] ?? createQuestionState(activeQuestion, nextLanguage);
    const nextFile = getFileNameForLanguage(nextLanguage);
    const nextCode = currentQuestionState.files[nextFile]?.content ?? activeQuestion?.starter_code[nextLanguage] ?? "";

    setQuestionStates({
      ...nextQuestionStates,
      [activeQuestionId]: {
        ...currentQuestionState,
        selected_language: nextLanguage,
        active_file: nextFile,
        files: {
          ...currentQuestionState.files,
          [nextFile]: {
            language: nextLanguage,
            content: nextCode
          }
        }
      }
    });
    setLanguage(nextLanguage);
    setCode(nextCode);
  }

  function switchQuestion(question: Question) {
    const nextQuestionStates = persistCurrentCode();
    const nextState = nextQuestionStates[question.question_id] ?? createQuestionState(question);
    const nextLanguage = nextState.selected_language;

    setQuestionStates({
      ...nextQuestionStates,
      [question.question_id]: nextState
    });
    setActiveQuestionId(question.question_id);
    setLanguage(nextLanguage);
    setCode(getCodeFromState(nextState, question, nextLanguage));
  }

  async function handleRun() {
    setRunState("running");
    setRunResult(null);
    setError(null);
    try {
      setRunResult(await runCode({
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        selected_language: language,
        active_file_content: code
      }));
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Run failed.");
    } finally {
      setRunState("idle");
    }
  }

  async function sendAi(level: AiHintLevel = selectedHintLevel, overrideMessage?: string) {
    setError(null);
    const message = (overrideMessage ?? aiMessage).trim() || getHintLabel(level);
    try {
      const response = await getAiResponse({
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        hint_level: level,
        message,
        selected_language: language,
        active_file_content: code
      });
      setMessages((current) => [
        ...current,
        { role: "student", text: `${getHintLabel(level)} hint: ${message}` },
        { role: "assistant", text: response.response_markdown }
      ]);
      setAiState((current) => current ? updateAiStateCredits(current, activeQuestionId, response.credits_remaining) : current);
      setQuestionStates((current) => ({
        ...current,
        [activeQuestionId]: {
          ...(current[activeQuestionId] ?? createQuestionState(activeQuestion, language)),
          ai_credits_remaining: response.credits_remaining
        }
      }));
      setAiMessage("");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "AI request failed.");
    }
  }

  async function submitFinal() {
    setConfirmSubmit(false);
    setError(null);
    setSaveState("saving");
    try {
      const nextQuestionStates = persistCurrentCode();
      setQuestionStates(nextQuestionStates);
      const savedWorkspace = await saveWorkspace(assessment.assessment_id, nextQuestionStates);
      setQuestionStates((current) => mergeQuestionStates(current, savedWorkspace.questions));
      setSaveState("saved");
      await finalizeSubmission(assessment.assessment_id);
      router.push(`/student/assessments/${assessment.assessment_id}/review`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Submission failed.");
      setSaveState("unsaved");
    }
  }

  const activeFile = getFileNameForLanguage(language);
  const aiQuestionState = aiState?.questions[activeQuestionId];
  const activeCreditsRemaining = aiQuestionState?.ai_credits_remaining ?? questionStates[activeQuestionId]?.ai_credits_remaining ?? activeQuestion?.ai_credit_budget ?? null;
  const activeCreditBudget = aiQuestionState?.ai_credit_budget ?? activeQuestion?.ai_credit_budget ?? null;
  const selectedHintCost = getHintCost(aiState, selectedHintLevel);
  const hasEnoughCredits = !aiState?.ai_settings.ai_credits_enabled || activeCreditsRemaining === null || activeCreditsRemaining >= selectedHintCost;
  const structuredHintsEnabled = Boolean(assessment.ai_enabled && (aiState?.ai_settings.structured_hints_enabled ?? assessment.ai_settings?.structured_hints_enabled ?? true));

  return (
    <div className="grid h-[calc(100vh-24px)] min-h-0 min-w-0 gap-2 lg:grid-cols-[minmax(220px,260px)_minmax(0,1fr)_minmax(220px,260px)] xl:grid-cols-[minmax(240px,280px)_minmax(0,1fr)_minmax(240px,280px)] 2xl:grid-cols-[minmax(260px,300px)_minmax(0,1fr)_minmax(260px,300px)]">
      <aside className="panel dynamic-surface flex min-h-0 min-w-0 flex-col rounded-xl p-3">
        <div className="relative flex items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs uppercase tracking-[0.16em] text-cyanGlow/70">Question list</p>
            <h2 className="mt-1 text-base font-semibold leading-snug">{assessment.title}</h2>
          </div>
          <span className="badge hidden shrink-0 xl:inline-flex">Active attempt</span>
        </div>
        <div className="relative mt-4 space-y-2">
          {assessment.questions.map((question, index) => (
            <button
              key={question.question_id}
              onClick={() => switchQuestion(question)}
              className={`dynamic-surface w-full rounded-xl border p-3 text-left transition ${
                question.question_id === activeQuestionId ? "border-cyanGlow/40 bg-cyanGlow/10 shadow-[0_0_18px_rgba(0,229,255,0.08)]" : "border-white/10 bg-white/5 hover:bg-white/10"
              }`}
            >
              <p className="text-xs text-white/40">Question {index + 1}</p>
              <p className="font-semibold text-white">{question.title}</p>
            </button>
          ))}
        </div>
        <div className="scrollbar-soft scanline relative mt-4 min-h-0 flex-1 overflow-y-auto rounded-xl border border-white/10 bg-black/20 p-3">
          <p className="text-xs uppercase tracking-[0.16em] text-white/35">Problem statement</p>
          <h3 className="mt-3 text-xl font-semibold text-white">{activeQuestion?.title}</h3>
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
      </aside>

      <section className="liquid-glass-neon flex min-h-0 min-w-0 flex-col rounded-xl">
        <div className="relative flex flex-wrap items-center gap-2 border-b border-white/10 p-3">
          <div className="min-w-0 flex-1">
            <p className="text-xs uppercase tracking-[0.16em] text-white/35">IDE workspace</p>
            <h1 className="truncate text-lg font-semibold xl:text-xl">{assessment.title}</h1>
          </div>
          <span className="badge hidden xl:inline-flex"><Clock size={14} /> 01:08:42</span>
          <span className="badge"><UploadCloud className={saveState === "saving" ? "animate-pulse" : ""} size={14} /> {saveState}</span>
          <button className="btn-primary px-4 py-2" onClick={() => setConfirmSubmit(true)}>Submit</button>
        </div>
        {error ? <p className="relative border-b border-white/10 px-4 py-2 text-sm text-pinkGlow">{error}</p> : null}
        <div className="relative flex flex-wrap items-center gap-2 border-b border-white/10 px-3 py-2">
          <span className="rounded-t-xl border border-b-0 border-cyanGlow/30 bg-black/30 px-4 py-2 font-mono text-xs text-cyanGlow shadow-[0_-8px_24px_rgba(0,229,255,0.06)]">
            {activeFile}
          </span>
          <label className="ml-auto hidden text-xs text-white/40 xl:inline" htmlFor="language">Language</label>
          <select id="language" className="field py-2" value={language} onChange={(event) => switchLanguage(event.target.value as Language)}>
            {STUDENT_LANGUAGES.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
          <button className="btn-secondary px-4 py-2" onClick={handleRun} disabled={runState === "running"}>
            <Play size={16} />
            {runState === "running" ? "Running..." : "Run"}
          </button>
        </div>
        <div className="relative grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_minmax(220px,28vh)]">
          <div className="min-h-0 overflow-hidden bg-black/30 p-3">
            <MonacoCodeEditor
              assessmentId={assessment.assessment_id}
              questionId={activeQuestionId}
              fileName={activeFile}
              language={language}
              value={code}
              onChange={updateCode}
            />
          </div>
          <div className="flex min-h-0 flex-col overflow-hidden border-t border-white/10 bg-black/25 p-3">
            <div className="mb-2 flex shrink-0 items-center justify-between">
              <h2 className="font-semibold">Output console</h2>
              <span className="text-xs text-white/40">Public/sample tests only</span>
            </div>
            <div className="scrollbar-soft min-h-0 flex-1 overflow-y-auto rounded-xl border border-white/10 bg-black/40 p-4 font-mono text-xs text-white/70">
              {runState === "running" ? <p className="text-cyanGlow">runner queued...</p> : null}
              {runResult ? (
                <div className="space-y-3">
                  <p className="text-cyanGlow">status: {runResult.status}</p>
                  <pre className="whitespace-pre-wrap">{runResult.stdout}</pre>
                  {runResult.test_results.map((test) => (
                    <p key={test.name}>{test.passed ? "PASS" : "FAIL"} {test.name}{test.output ? `: ${test.output}` : ""}</p>
                  ))}
                  <p className="text-white/40">cpu {runResult.metrics.cpu_time_seconds}s, memory {runResult.metrics.peak_memory_kb}kb</p>
                </div>
              ) : (
                <p className="text-white/35">Run code to see stdout, stderr, and public test results. Hidden tests are not shown here.</p>
              )}
            </div>
            {buildRunFailureSummary(runResult, error) ? (
              <div className="mt-2 shrink-0 rounded-xl border border-cyanGlow/20 bg-cyanGlow/5 px-3 py-2">
                <p className="text-[11px] uppercase tracking-[0.16em] text-cyanGlow/70">AI suggestion</p>
                <p className="mt-1 text-xs leading-5 text-white/65">
                  The last run exposed a problem. Ask the assistant for a focused debugging hint.
                </p>
                <button className="btn-secondary mt-2 px-3 py-1.5 text-xs" onClick={() => sendAi("debugging_hint", buildDebugPrompt(runResult, error))} disabled={!structuredHintsEnabled}>
                  <Sparkles size={14} />
                  Ask AI to debug this run
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </section>

      <aside className="panel dynamic-surface flex min-h-0 min-w-0 flex-col rounded-xl p-3">
        <div className="relative flex items-center gap-3">
          <span className="float-soft grid h-10 w-10 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow"><Brain size={20} /></span>
          <div className="min-w-0">
            <h2 className="font-semibold">AI assistant</h2>
            <p className="text-xs text-white/40">{structuredHintsEnabled ? "Structured hints enabled" : "Disabled for this assessment"}</p>
          </div>
        </div>
        <div className="relative mt-4 rounded-xl border border-white/10 bg-black/20 p-3">
          <div className="flex items-center justify-between gap-3">
            <span className="text-xs uppercase tracking-[0.16em] text-white/35">Credits</span>
            <span className="font-mono text-sm text-cyanGlow">
              {activeCreditsRemaining ?? "-"}{activeCreditBudget !== null ? ` / ${activeCreditBudget}` : ""}
            </span>
          </div>
        </div>
        <div className="relative mt-3 grid grid-cols-1 gap-2 xl:grid-cols-2">
          {HINT_LEVELS.map((hint) => {
            const cost = getHintCost(aiState, hint.value);
            const disabled = !structuredHintsEnabled || (aiState?.ai_settings.ai_credits_enabled && activeCreditsRemaining !== null && activeCreditsRemaining < cost);
            return (
              <button
                key={hint.value}
                disabled={disabled}
                className={`btn-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-45 ${selectedHintLevel === hint.value ? "border-cyanGlow/50 bg-cyanGlow/10" : ""}`}
                onClick={() => setSelectedHintLevel(hint.value)}
              >
                <span className="grid w-full grid-cols-[18px_minmax(0,1fr)_auto] items-center gap-2 text-left">
                  <Sparkles size={14} className="justify-self-center" />
                  <span className="block truncate">{hint.label}</span>
                  <span className="font-mono text-[11px] text-white/45">{cost}</span>
                </span>
              </button>
            );
          })}
        </div>
        {!hasEnoughCredits ? (
          <p className="relative mt-2 text-xs leading-5 text-pinkGlow">Not enough credits for the selected hint level.</p>
        ) : null}
        <div className="relative mt-3 flex gap-2 rounded-2xl border border-white/10 bg-black/20 p-2">
          <input
            className="min-w-0 flex-1 bg-transparent px-2 text-sm text-white outline-none placeholder:text-white/30"
            placeholder="Add context for the selected hint..."
            value={aiMessage}
            onChange={(event) => setAiMessage(event.target.value)}
            disabled={!structuredHintsEnabled}
          />
          <button
            className="rounded-xl bg-cyanGlow p-2 text-slate-950 disabled:cursor-not-allowed disabled:opacity-45"
            onClick={() => sendAi(selectedHintLevel)}
            disabled={!structuredHintsEnabled || !hasEnoughCredits}
          >
            <Send size={16} />
          </button>
        </div>
        <div className="scrollbar-soft relative mt-4 min-h-0 flex-1 space-y-3 overflow-y-auto rounded-xl border border-white/10 bg-black/20 p-3">
          {messages.map((message, index) => (
            <div key={`${message.role}-${index}`} className={`reveal-up rounded-2xl p-3 text-sm leading-6 ${message.role === "assistant" ? "bg-white/5 text-white/70" : "bg-cyanGlow/10 text-cyanGlow"}`}>
              {renderMarkdown(message.text)}
            </div>
          ))}
        </div>
      </aside>

      {confirmSubmit ? (
        <Modal title="Submit final solution" onClose={() => setConfirmSubmit(false)}>
          <p className="text-white/60">This submits the current attempt. Hidden test input and expected output remain private.</p>
          <div className="mt-6 flex justify-end gap-3">
            <button className="btn-secondary" onClick={() => setConfirmSubmit(false)}>Cancel</button>
            <button className="btn-primary" onClick={submitFinal}>Submit result</button>
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
          <button className="rounded-xl border border-white/10 bg-white/5 p-2 text-white/60 hover:text-white" onClick={onClose}><X size={18} /></button>
        </div>
        <div className="relative mt-4">{children}</div>
      </section>
    </div>
  );
}
