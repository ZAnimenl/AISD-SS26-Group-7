"use client";

import { useEffect, useMemo, useState } from "react";
import { Brain, CheckCircle2, Clock, Code2, Play, Send, Sparkles, UploadCloud, X } from "lucide-react";
import { autosaveWorkspace, finalizeSubmission, getAiResponse, runCode } from "@/lib/api";
import type { AiInteractionType, Assessment, Language, RunResult, SubmissionResult, WorkspaceState } from "@/lib/types";

interface WorkspaceClientProps {
  assessment: Assessment;
  workspace: WorkspaceState;
  sessionId: string;
}

type SaveState = "saved" | "unsaved" | "saving";

export function WorkspaceClient({ assessment, workspace, sessionId }: WorkspaceClientProps) {
  const firstQuestion = assessment.questions[0];
  const [activeQuestionId, setActiveQuestionId] = useState(firstQuestion?.question_id ?? "q-two-sum");
  const [questionStates, setQuestionStates] = useState(workspace.questions);
  const activeQuestion = useMemo(
    () => assessment.questions.find((question) => question.question_id === activeQuestionId) ?? assessment.questions[0],
    [assessment.questions, activeQuestionId]
  );
  const initialState = questionStates[activeQuestionId] ?? Object.values(questionStates)[0];
  const [language, setLanguage] = useState<Language>(initialState?.selected_language ?? "python");
  const [code, setCode] = useState(initialState?.files[initialState.active_file]?.content ?? activeQuestion?.starter_code.python ?? "");
  const [saveState, setSaveState] = useState<SaveState>("saved");
  const [runState, setRunState] = useState<"idle" | "running">("idle");
  const [runResult, setRunResult] = useState<RunResult | null>(null);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [submission, setSubmission] = useState<SubmissionResult | null>(null);
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
        const activeFile = language === "javascript" ? "main.js" : "main.py";
        const savedWorkspace = await autosaveWorkspace(
          sessionId,
          activeQuestionId,
          language,
          activeFile,
          code
        );
        setQuestionStates(savedWorkspace.questions);
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
  }, [activeQuestionId, code, language, sessionId]);

  function switchLanguage(nextLanguage: Language) {
    setLanguage(nextLanguage);
    setCode(activeQuestion?.starter_code[nextLanguage] ?? "");
  }

  async function handleRun() {
    setRunState("running");
    setRunResult(null);
    setError(null);
    try {
      setRunResult(await runCode({
        session_id: sessionId,
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

  async function sendAi(type: AiInteractionType) {
    setError(null);
    const message = aiMessage.trim() || type.replace("_", " ");
    try {
      const response = await getAiResponse({
        session_id: sessionId,
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
    try {
      setSubmission(await finalizeSubmission(sessionId));
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Submission failed.");
    }
  }

  return (
    <div className="grid h-[calc(100vh-140px)] min-h-[780px] gap-4 xl:grid-cols-[320px_minmax(420px,1fr)_340px]">
      <aside className="panel flex min-h-0 flex-col">
        <div className="relative flex items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-[0.16em] text-cyanGlow/70">Question list</p>
            <h2 className="mt-1 text-lg font-semibold">{assessment.title}</h2>
          </div>
          <span className="badge">Backend session</span>
        </div>
        <div className="relative mt-4 space-y-2">
          {assessment.questions.map((question, index) => (
            <button
              key={question.question_id}
              onClick={() => {
                const activeFile = language === "javascript" ? "main.js" : "main.py";
                setQuestionStates((current) => ({
                  ...current,
                  [activeQuestionId]: {
                    selected_language: language,
                    active_file: activeFile,
                    files: { [activeFile]: { language, content: code } },
                    last_saved_at: new Date().toISOString(),
                    version: current[activeQuestionId]?.version ?? 0
                  }
                }));
                const nextState = questionStates[question.question_id];
                setActiveQuestionId(question.question_id);
                setLanguage(nextState?.selected_language ?? "python");
                setCode(nextState?.files[nextState.active_file]?.content ?? question.starter_code.python ?? "");
              }}
              className={`w-full rounded-xl border p-3 text-left transition ${
                question.question_id === activeQuestionId ? "border-cyanGlow/40 bg-cyanGlow/10" : "border-white/10 bg-white/5 hover:bg-white/10"
              }`}
            >
              <p className="text-xs text-white/40">Question {index + 1}</p>
              <p className="font-semibold text-white">{question.title}</p>
            </button>
          ))}
        </div>
        <div className="scrollbar-soft relative mt-4 min-h-0 flex-1 overflow-y-auto rounded-2xl border border-white/10 bg-black/20 p-4">
          <p className="text-xs uppercase tracking-[0.16em] text-white/35">Problem statement</p>
          <h3 className="mt-3 text-xl font-semibold text-white">{activeQuestion?.title}</h3>
          <p className="mt-3 leading-7 text-white/65">{activeQuestion?.problem_description_markdown}</p>
          <h4 className="mt-6 text-sm font-semibold text-cyanGlow">Constraints</h4>
          <ul className="mt-2 space-y-2 text-sm text-white/55">
            {activeQuestion?.constraints.map((constraint) => <li key={constraint}>- {constraint}</li>)}
          </ul>
          <h4 className="mt-6 text-sm font-semibold text-cyanGlow">Public examples</h4>
          <div className="mt-2 space-y-2">
            {activeQuestion?.public_examples.map((example) => (
              <div key={example.test_case_id} className="rounded-xl border border-white/10 bg-white/5 p-3 text-xs text-white/55">
                <p className="text-white/80">{example.name}</p>
                <p>Input: {example.input}</p>
                <p>Expected: {example.expected_output}</p>
              </div>
            ))}
          </div>
        </div>
      </aside>

      <section className="liquid-glass-neon flex min-h-0 flex-col rounded-2xl">
        <div className="relative flex flex-wrap items-center gap-3 border-b border-white/10 p-4">
          <div className="mr-auto">
            <p className="text-xs uppercase tracking-[0.16em] text-white/35">IDE workspace</p>
            <h1 className="text-xl font-semibold">{assessment.title}</h1>
          </div>
          <span className="badge"><Clock size={14} /> 01:08:42</span>
          <span className="badge"><UploadCloud size={14} /> {saveState}</span>
          <button className="btn-primary" onClick={() => setConfirmSubmit(true)}>Submit</button>
        </div>
        {error ? <p className="relative border-b border-white/10 px-4 py-2 text-sm text-pinkGlow">{error}</p> : null}
        <div className="relative flex flex-wrap items-center gap-3 border-b border-white/10 px-4 py-3">
          <span className="rounded-t-xl border border-b-0 border-cyanGlow/30 bg-black/30 px-4 py-2 font-mono text-xs text-cyanGlow">
            {language === "python" ? "main.py" : "main.js"}
          </span>
          <label className="ml-auto text-xs text-white/40" htmlFor="language">Language</label>
          <select id="language" className="field py-2" value={language} onChange={(event) => switchLanguage(event.target.value as Language)}>
            <option value="python">Python</option>
            <option value="javascript">JavaScript</option>
          </select>
          <button className="btn-secondary" onClick={handleRun} disabled={runState === "running"}>
            <Play size={16} />
            {runState === "running" ? "Running..." : "Run"}
          </button>
        </div>
        <div className="relative grid min-h-0 flex-1 grid-rows-[1fr_240px]">
          <div className="min-h-0 bg-black/30 p-4">
            <div className="mb-2 flex items-center gap-2 text-xs text-white/35">
              <Code2 size={14} />
              <span>TODO(Monaco): replace textarea fallback with Monaco Editor when dependency is approved.</span>
            </div>
            <textarea
              className="scrollbar-soft h-full w-full resize-none rounded-2xl border border-white/10 bg-[#080b14] p-4 font-mono text-sm leading-7 text-white/85 outline-none focus:border-cyanGlow/60"
              value={code}
              spellCheck={false}
              onChange={(event) => setCode(event.target.value)}
            />
          </div>
          <div className="border-t border-white/10 bg-black/25 p-4">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="font-semibold">Output console</h2>
              <span className="text-xs text-white/40">Public/sample tests only</span>
            </div>
            <div className="scrollbar-soft h-[168px] overflow-y-auto rounded-2xl border border-white/10 bg-black/40 p-4 font-mono text-xs text-white/70">
              {runState === "running" ? <p className="text-cyanGlow">runner queued...</p> : null}
              {runResult ? (
                <div className="space-y-3">
                  <p className="text-cyanGlow">status: {runResult.status}</p>
                  <pre className="whitespace-pre-wrap">{runResult.stdout}</pre>
                  {runResult.test_results.map((test) => (
                    <p key={test.name}>{test.passed ? "PASS" : "FAIL"} {test.name}: actual {test.actual_output}, expected {test.expected_output}</p>
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

      <aside className="panel flex min-h-0 flex-col">
        <div className="relative flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow"><Brain size={20} /></span>
          <div>
            <h2 className="font-semibold">AI assistant</h2>
            <p className="text-xs text-white/40">{assessment.ai_enabled ? "Backend responses enabled" : "Disabled for this assessment"}</p>
          </div>
        </div>
        <div className="relative mt-4 grid grid-cols-2 gap-2">
          {(["hint", "explain", "debug", "code_review"] as AiInteractionType[]).map((type) => (
            <button key={type} disabled={!assessment.ai_enabled} className="btn-secondary px-3 py-2 text-xs" onClick={() => sendAi(type)}>
              <Sparkles size={14} />
              {type.replace("_", " ")}
            </button>
          ))}
        </div>
        <div className="scrollbar-soft relative mt-4 min-h-0 flex-1 space-y-3 overflow-y-auto rounded-2xl border border-white/10 bg-black/20 p-3">
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
          <p className="text-white/60">This submits the current backend session. Hidden test input and expected output remain private.</p>
          <div className="mt-6 flex justify-end gap-3">
            <button className="btn-secondary" onClick={() => setConfirmSubmit(false)}>Cancel</button>
            <button className="btn-primary" onClick={submitFinal}>Submit result</button>
          </div>
        </Modal>
      ) : null}

      {submission ? (
        <Modal title="Submission received" onClose={() => setSubmission(null)}>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><CheckCircle2 className="text-cyanGlow" /><p className="mt-2 text-2xl font-semibold">{submission.score}/{submission.max_score}</p><p className="text-xs text-white/45">Score</p></div>
            <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><p className="text-2xl font-semibold">{submission.visible_test_summary.passed}/{submission.visible_test_summary.total}</p><p className="text-xs text-white/45">Public tests</p></div>
            <div className="rounded-2xl border border-white/10 bg-white/5 p-4"><p className="text-2xl font-semibold">{submission.hidden_test_summary.passed}/{submission.hidden_test_summary.total}</p><p className="text-xs text-white/45">Hidden summary only</p></div>
          </div>
          <p className="mt-5 rounded-2xl border border-white/10 bg-black/20 p-4 text-sm text-white/60">{submission.stdout}</p>
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
