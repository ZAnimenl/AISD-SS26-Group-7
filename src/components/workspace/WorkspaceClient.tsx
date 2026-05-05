"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { Brain, Clock, Play, Send, Sparkles, UploadCloud, X } from "lucide-react";
import { autosaveWorkspace, finalizeSubmission, getAiResponse, runCode, saveWorkspace } from "@/lib/api";
import { MonacoCodeEditor } from "@/components/workspace/MonacoCodeEditor";
import type { AiInteractionType, Assessment, Language, Question, RunResult, WorkspaceQuestionState, WorkspaceState } from "@/lib/types";

interface WorkspaceClientProps {
  assessment: Assessment;
  workspace: WorkspaceState;
  backendAttemptId: string;
}

type SaveState = "saved" | "unsaved" | "saving";

const STUDENT_LANGUAGES: Array<{ value: Language; label: string }> = [
  { value: "python", label: "Python" },
  { value: "javascript", label: "JavaScript" }
];

function getFileNameForLanguage(language: Language) {
  return language === "javascript" ? "main.js" : "main.py";
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
    version: 0
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

export function WorkspaceClient({ assessment, workspace, backendAttemptId }: WorkspaceClientProps) {
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
  const [aiMessage, setAiMessage] = useState("");
  const [messages, setMessages] = useState([
    { role: "assistant", text: "I can give hints, explain concepts, debug symptoms, or review your current approach through the backend AI endpoint." }
  ]);

  useEffect(() => {
    setSaveState("unsaved");
    const saving = window.setTimeout(() => setSaveState("saving"), 450);
    const saved = window.setTimeout(async () => {
      try {
        const activeFile = getFileNameForLanguage(language);
        const savedWorkspace = await autosaveWorkspace(
          backendAttemptId,
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
  }, [activeQuestionId, backendAttemptId, code, language]);

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
        backend_attempt_id: backendAttemptId,
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

  async function sendAi(type: AiInteractionType, overrideMessage?: string) {
    setError(null);
    const message = (overrideMessage ?? aiMessage).trim() || type.replace("_", " ");
    try {
      const response = await getAiResponse({
        backend_attempt_id: backendAttemptId,
        assessment_id: assessment.assessment_id,
        question_id: activeQuestionId,
        interaction_type: type,
        message,
        selected_language: language,
        active_file_content: code
      });
      setMessages((current) => [
        ...current,
        { role: "student", text: message },
        { role: "assistant", text: response }
      ]);
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
      const savedWorkspace = await saveWorkspace(backendAttemptId, nextQuestionStates);
      setQuestionStates((current) => mergeQuestionStates(current, savedWorkspace.questions));
      setSaveState("saved");
      await finalizeSubmission(backendAttemptId);
      router.push(`/student/assessments/${assessment.assessment_id}/review`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Submission failed.");
      setSaveState("unsaved");
    }
  }

  const activeFile = getFileNameForLanguage(language);

  return (
    <div className="grid h-[calc(100vh-24px)] min-h-0 min-w-0 gap-2 lg:grid-cols-[minmax(220px,260px)_minmax(0,1fr)_minmax(220px,260px)] xl:grid-cols-[minmax(240px,280px)_minmax(0,1fr)_minmax(240px,280px)] 2xl:grid-cols-[minmax(260px,300px)_minmax(0,1fr)_minmax(260px,300px)]">
      <aside className="panel flex min-h-0 min-w-0 flex-col rounded-xl p-3">
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
              className={`w-full rounded-xl border p-3 text-left transition ${
                question.question_id === activeQuestionId ? "border-cyanGlow/40 bg-cyanGlow/10" : "border-white/10 bg-white/5 hover:bg-white/10"
              }`}
            >
              <p className="text-xs text-white/40">Question {index + 1}</p>
              <p className="font-semibold text-white">{question.title}</p>
            </button>
          ))}
        </div>
        <div className="scrollbar-soft relative mt-4 min-h-0 flex-1 overflow-y-auto rounded-xl border border-white/10 bg-black/20 p-3">
          <p className="text-xs uppercase tracking-[0.16em] text-white/35">Problem statement</p>
          <h3 className="mt-3 text-xl font-semibold text-white">{activeQuestion?.title}</h3>
          <p className="mt-3 leading-7 text-white/65">{activeQuestion?.problem_description_markdown}</p>
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
          <span className="badge"><UploadCloud size={14} /> {saveState}</span>
          <button className="btn-primary px-4 py-2" onClick={() => setConfirmSubmit(true)}>Submit</button>
        </div>
        {error ? <p className="relative border-b border-white/10 px-4 py-2 text-sm text-pinkGlow">{error}</p> : null}
        <div className="relative flex flex-wrap items-center gap-2 border-b border-white/10 px-3 py-2">
          <span className="rounded-t-xl border border-b-0 border-cyanGlow/30 bg-black/30 px-4 py-2 font-mono text-xs text-cyanGlow">
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
        <div className="relative grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_176px]">
          <div className="min-h-0 bg-black/30 p-3">
            <MonacoCodeEditor
              assessmentId={assessment.assessment_id}
              questionId={activeQuestionId}
              fileName={activeFile}
              language={language}
              value={code}
              onChange={updateCode}
            />
          </div>
          <div className="border-t border-white/10 bg-black/25 p-3">
            <div className="mb-2 flex items-center justify-between">
              <h2 className="font-semibold">Output console</h2>
              <span className="text-xs text-white/40">Public/sample tests only</span>
            </div>
            {buildRunFailureSummary(runResult, error) ? (
              <div className="mb-3 rounded-xl border border-cyanGlow/20 bg-cyanGlow/5 p-3">
                <p className="text-xs uppercase tracking-[0.16em] text-cyanGlow/70">AI suggestion</p>
                <p className="mt-2 text-sm leading-6 text-white/70">
                  The last run exposed a problem. Ask the assistant to debug the current output and code path.
                </p>
                <button className="btn-secondary mt-3 px-3 py-2 text-xs" onClick={() => sendAi("debug", buildDebugPrompt(runResult, error))}>
                  <Sparkles size={14} />
                  Ask AI to debug this run
                </button>
              </div>
            ) : null}
            <div className="scrollbar-soft h-[116px] overflow-y-auto rounded-xl border border-white/10 bg-black/40 p-4 font-mono text-xs text-white/70">
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
          </div>
        </div>
      </section>

      <aside className="panel flex min-h-0 min-w-0 flex-col rounded-xl p-3">
        <div className="relative flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow"><Brain size={20} /></span>
          <div className="min-w-0">
            <h2 className="font-semibold">AI assistant</h2>
            <p className="text-xs text-white/40">{assessment.ai_enabled ? "Available for this assessment" : "Disabled for this assessment"}</p>
          </div>
        </div>
        <div className="relative mt-4 grid grid-cols-1 gap-2 xl:grid-cols-2">
          {(["hint", "explain", "debug", "code_review"] as AiInteractionType[]).map((type) => (
            <button key={type} disabled={!assessment.ai_enabled} className="btn-secondary px-3 py-2 text-xs" onClick={() => sendAi(type)}>
              <Sparkles size={14} />
              {type.replace("_", " ")}
            </button>
          ))}
        </div>
        <div className="scrollbar-soft relative mt-4 min-h-0 flex-1 space-y-3 overflow-y-auto rounded-xl border border-white/10 bg-black/20 p-3">
          {messages.map((message, index) => (
            <div key={`${message.role}-${index}`} className={`rounded-2xl p-3 text-sm leading-6 ${message.role === "assistant" ? "bg-white/5 text-white/70" : "bg-cyanGlow/10 text-cyanGlow"}`}>
              {message.text}
            </div>
          ))}
        </div>
        <div className="relative mt-4 flex gap-2 rounded-2xl border border-white/10 bg-black/20 p-2">
          <input
            className="min-w-0 flex-1 bg-transparent px-2 text-sm text-white outline-none placeholder:text-white/30"
            placeholder="Ask a question..."
            value={aiMessage}
            onChange={(event) => setAiMessage(event.target.value)}
          />
          <button className="rounded-xl bg-cyanGlow p-2 text-slate-950" onClick={() => sendAi("chat")}><Send size={16} /></button>
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
