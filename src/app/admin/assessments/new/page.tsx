"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, Check, ChevronDown, FileText, Loader2, Minus, Plus, Settings2, Wand2 } from "lucide-react";
import { createAssessment, generateAssessment } from "@/lib/api";
import { toLocalDateTimeInput, toUtcIso } from "@/lib/assessmentSchedule";
import { SectionHeader } from "@/components/ui/SectionHeader";
import type { Difficulty, TaskType } from "@/lib/types";

type CreateMode = "manual" | "generate";
type CreateStep = 1 | 2;

const MAX_TASKS_PER_TYPE = 5;
const MAX_TOTAL_TASKS = 12;
const difficultyOptions: Difficulty[] = ["easy", "medium", "hard"];
const taskBlueprints: Array<{ type: TaskType; label: string; description: string }> = [
  { type: "frontend_ui_extension", label: "Frontend UI extension", description: "Browser UI and interaction work" },
  { type: "rest_api_development", label: "REST API development", description: "Todo endpoints and service logic" },
  { type: "database_query_schema", label: "Database query/schema", description: "Schema, migration, and reporting work" },
  { type: "bug_fix", label: "Bug fix", description: "Cross-file diagnosis and regression repair" }
];

export default function NewAssessmentPage() {
  const [step, setStep] = useState<CreateStep>(1);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pendingMode, setPendingMode] = useState<CreateMode | null>(null);
  const [configurationReady, setConfigurationReady] = useState(false);
  const [startMode, setStartMode] = useState<"now" | "scheduled">("now");
  const [scheduledStart, setScheduledStart] = useState(() => toLocalDateTimeInput());
  const [difficulty, setDifficulty] = useState<Difficulty>("medium");
  const [difficultyMenuOpen, setDifficultyMenuOpen] = useState(false);
  const [taskCounts, setTaskCounts] = useState<Record<TaskType, number>>({
    frontend_ui_extension: 1,
    rest_api_development: 1,
    database_query_schema: 1,
    bug_fix: 1
  });
  const titleRef = useRef<HTMLInputElement>(null);
  const descriptionRef = useRef<HTMLTextAreaElement>(null);
  const difficultyMenuRef = useRef<HTMLDivElement>(null);
  const formRef = useRef<HTMLFormElement>(null);
  const router = useRouter();
  const isPending = pendingMode !== null;
  const totalTasks = Object.values(taskCounts).reduce((sum, count) => sum + count, 0);

  useEffect(() => {
    function closeDifficultyMenu(event: MouseEvent) {
      if (!difficultyMenuRef.current?.contains(event.target as Node)) {
        setDifficultyMenuOpen(false);
      }
    }

    document.addEventListener("mousedown", closeDifficultyMenu);
    return () => document.removeEventListener("mousedown", closeDifficultyMenu);
  }, []);

  useEffect(() => {
    if (step !== 2) {
      return;
    }

    const timer = window.setTimeout(() => setConfigurationReady(true), 500);
    return () => window.clearTimeout(timer);
  }, [step]);

  function updateTaskCount(taskType: TaskType, requestedCount: number) {
    setTaskCounts((current) => {
      const currentTotal = Object.values(current).reduce((sum, count) => sum + count, 0);
      const maximumForType = Math.min(MAX_TASKS_PER_TYPE, current[taskType] + (MAX_TOTAL_TASKS - currentTotal));
      return {
        ...current,
        [taskType]: Math.max(0, Math.min(maximumForType, requestedCount))
      };
    });
  }

  function advanceToConfiguration() {
    if (!titleRef.current?.reportValidity() || !descriptionRef.current?.reportValidity()) {
      return;
    }

    setError(null);
    setConfigurationReady(false);
    setStep(2);
  }

  async function createFromConfiguration(mode: CreateMode) {
    if (!configurationReady || isPending || !formRef.current) {
      return;
    }

    const form = new FormData(formRef.current);
    const shouldGenerate = mode === "generate";
    setError(null);
    setPendingMode(mode);
    try {
      const create = shouldGenerate ? generateAssessment : createAssessment;
      const createdAssessment = await create({
        title: title.trim(),
        description: description.trim(),
        duration_minutes: Number(form.get("duration_minutes") ?? 50),
        starts_at: startMode === "scheduled" ? toUtcIso(scheduledStart) : null,
        status: (shouldGenerate ? "draft" : String(form.get("status") ?? "draft")) as any,
        ai_enabled: form.get("ai_enabled") === "enabled",
        shared_prototype_reference: "default-todo-list",
        shared_prototype_version: "1.0",
        task_type_counts: taskCounts,
        difficulty
      });
      router.push(`/admin/assessments/${createdAssessment.assessment_id}#questions`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to create assessment.");
    } finally {
      setPendingMode(null);
    }
  }

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Create assessment" />
      <section className="panel max-w-4xl">
        <form
          ref={formRef}
          className="relative grid gap-5"
          onSubmit={(event) => {
            event.preventDefault();
            if (step === 1) {
              advanceToConfiguration();
            }
          }}
        >
          <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
            <ol className="grid grid-cols-[1fr_auto_1fr] items-center gap-3" aria-label="Assessment creation progress">
              <li>
                <button
                  type="button"
                  className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left transition ${
                    step === 1 ? "bg-cyanGlow/10 text-white" : "text-white/55 hover:bg-white/5 hover:text-white/80"
                  }`}
                  aria-current={step === 1 ? "step" : undefined}
                  onClick={() => {
                    setError(null);
                    setConfigurationReady(false);
                    setStep(1);
                  }}
                >
                  <span className={`grid h-9 w-9 shrink-0 place-items-center rounded-full border ${
                    step === 1 ? "border-cyanGlow/60 bg-cyanGlow/15 text-cyanGlow" : "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow"
                  }`}>
                    {step === 2 ? <Check size={16} /> : <FileText size={16} />}
                  </span>
                  <span className="min-w-0">
                    <span className="block text-[10px] uppercase tracking-[0.14em] text-white/35">Step 1</span>
                    <span className="block truncate text-sm font-medium">Assessment basics</span>
                  </span>
                </button>
              </li>
              <li aria-hidden="true" className={`h-px w-8 sm:w-16 ${step === 2 ? "bg-cyanGlow/55" : "bg-white/15"}`} />
              <li>
                <div
                  className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 ${
                    step === 2 ? "bg-cyanGlow/10 text-white" : "text-white/40"
                  }`}
                  aria-current={step === 2 ? "step" : undefined}
                >
                  <span className={`grid h-9 w-9 shrink-0 place-items-center rounded-full border ${
                    step === 2 ? "border-cyanGlow/60 bg-cyanGlow/15 text-cyanGlow" : "border-white/15 bg-white/5 text-white/35"
                  }`}>
                    <Settings2 size={16} />
                  </span>
                  <span className="min-w-0">
                    <span className="block text-[10px] uppercase tracking-[0.14em] text-white/35">Step 2</span>
                    <span className="block truncate text-sm font-medium">Blueprint & settings</span>
                  </span>
                </div>
              </li>
            </ol>
          </div>

          {step === 1 ? (
            <div className="grid gap-6 rounded-2xl border border-white/10 bg-black/15 p-5 sm:p-6">
              <div>
                <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Assessment basics</p>
                <h2 className="mt-1 text-xl font-semibold">What is this assessment about?</h2>
                <p className="mt-1 text-sm text-white/40">Give students a clear title and enough context before configuring the tasks.</p>
              </div>
              <label className="grid gap-2 text-sm text-white/60">
                Title
                <input
                  ref={titleRef}
                  className="field"
                  name="title"
                  value={title}
                  required
                  autoFocus
                  placeholder="e.g. Advanced Todo Application Engineering"
                  onChange={(event) => setTitle(event.target.value)}
                />
              </label>
              <label className="grid gap-2 text-sm text-white/60">
                Description
                <textarea
                  ref={descriptionRef}
                  className="field min-h-36"
                  name="description"
                  value={description}
                  required
                  placeholder="Describe the assessment goals, expected skills, and overall scenario."
                  onChange={(event) => setDescription(event.target.value)}
                />
              </label>
            </div>
          ) : (
            <div className="flex flex-col gap-5">
              <div className="order-2 grid gap-5 rounded-2xl border border-white/10 bg-black/15 p-5">
                <div>
                  <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Assessment settings</p>
                  <h2 className="mt-1 text-lg font-semibold">Set the delivery options</h2>
                  <p className="mt-1 text-sm text-white/40">Configure timing, availability, and AI access for the assessment.</p>
                </div>
                <div className="grid gap-4 sm:grid-cols-2">
                  <label className="grid gap-2 text-sm text-white/60">
                    Duration (minutes)
                    <input className="field" name="duration_minutes" type="number" min={10} defaultValue={50} />
                  </label>
                  <div className="grid gap-2 text-sm text-white/60">
                    Starts
                    <div className="grid grid-cols-2 rounded-xl border border-white/10 bg-black/20 p-1">
                      {(["now", "scheduled"] as const).map((mode) => (
                        <button
                          key={mode}
                          type="button"
                          className={`rounded-lg px-3 py-2 text-sm font-medium transition ${
                            startMode === mode ? "bg-cyanGlow/15 text-cyanGlow" : "text-white/45 hover:text-white/75"
                          }`}
                          aria-pressed={startMode === mode}
                          onClick={() => setStartMode(mode)}
                        >
                          {mode === "now" ? "Now" : "Schedule"}
                        </button>
                      ))}
                    </div>
                  </div>
                  {startMode === "scheduled" ? (
                    <label className="grid gap-2 text-sm text-white/60 sm:col-span-2">
                      Start date and time
                      <input
                        className="field [color-scheme:dark]"
                        type="datetime-local"
                        value={scheduledStart}
                        min={toLocalDateTimeInput(new Date().toISOString())}
                        required
                        onChange={(event) => setScheduledStart(event.target.value)}
                      />
                      <span className="text-xs text-white/35">Uses your current local timezone and is synchronized for every user.</span>
                    </label>
                  ) : null}
                  <label className="grid gap-2 text-sm text-white/60">
                    Status
                    <select className="field" name="status" defaultValue="draft"><option>draft</option><option>active</option><option>closed</option><option>archived</option></select>
                  </label>
                  <label className="grid gap-2 text-sm text-white/60">
                    AI assistance
                    <select className="field" name="ai_enabled" defaultValue="enabled"><option>enabled</option><option>disabled</option></select>
                  </label>
                </div>
              </div>

              <div className="order-1 rounded-2xl border border-white/10 bg-black/20 p-5">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question blueprint</p>
                    <h2 className="mt-1 text-lg font-semibold">Choose the assessment mix</h2>
                    <p className="mt-1 text-sm text-white/45">Set how many Todo-prototype tasks to generate in each category.</p>
                  </div>
                  <div className="grid min-w-[124px] place-items-center rounded-xl border border-cyanGlow/25 bg-cyanGlow/10 px-4 py-2 text-center">
                    <p className="text-[10px] uppercase tracking-[0.14em] text-white/40">Total tasks</p>
                    <p className="text-2xl font-semibold text-cyanGlow">{totalTasks}</p>
                  </div>
                </div>

                <div className="mt-5 grid gap-3 lg:grid-cols-2">
                  {taskBlueprints.map((blueprint) => {
                    const count = taskCounts[blueprint.type];
                    const canIncrease = totalTasks < MAX_TOTAL_TASKS && count < MAX_TASKS_PER_TYPE;
                    return (
                      <div key={blueprint.type} className="rounded-xl border border-white/10 bg-white/[0.035] p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <p className="font-medium text-white/85">{blueprint.label}</p>
                            <p className="mt-1 text-xs text-white/40">{blueprint.description}</p>
                          </div>
                          <span className="grid h-9 min-w-9 place-items-center rounded-lg border border-cyanGlow/25 bg-cyanGlow/10 px-2 font-mono text-sm text-cyanGlow">{count}</span>
                        </div>
                        <div className="mt-4 grid grid-cols-[36px_1fr_36px] items-center gap-3">
                          <button
                            className="grid h-9 w-9 place-items-center rounded-full border border-white/15 bg-black/20 text-white/65 transition hover:border-cyanGlow/45 hover:text-cyanGlow disabled:opacity-30"
                            type="button"
                            aria-label={`Remove one ${blueprint.label} task`}
                            disabled={isPending || count === 0}
                            onClick={() => updateTaskCount(blueprint.type, count - 1)}
                          >
                            <Minus size={15} />
                          </button>
                          <input
                            className="h-2 w-full cursor-pointer accent-cyan-400"
                            type="range"
                            min={0}
                            max={MAX_TASKS_PER_TYPE}
                            value={count}
                            aria-label={`${blueprint.label} question count`}
                            disabled={isPending}
                            onChange={(event) => updateTaskCount(blueprint.type, Number(event.target.value))}
                          />
                          <button
                            className="grid h-9 w-9 place-items-center rounded-full border border-white/15 bg-black/20 text-white/65 transition hover:border-cyanGlow/45 hover:text-cyanGlow disabled:opacity-30"
                            type="button"
                            aria-label={`Add one ${blueprint.label} task`}
                            disabled={isPending || !canIncrease}
                            onClick={() => updateTaskCount(blueprint.type, count + 1)}
                          >
                            <Plus size={15} />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>

                <div className="mt-4 grid gap-4 rounded-xl border border-white/10 bg-black/20 p-4 sm:grid-cols-[1fr_auto] sm:items-center">
                  <div>
                    <p className="text-sm font-medium text-white/80">Shared difficulty</p>
                    <p className="mt-1 text-xs leading-5 text-white/40">Applied to every generated task. You can fine-tune individual tasks afterward.</p>
                  </div>
                  <div ref={difficultyMenuRef} className="relative w-full sm:w-40 sm:justify-self-end">
                    <button
                      type="button"
                      className="flex w-full items-center justify-between rounded-xl border border-white/15 bg-[#121522] px-3.5 py-2.5 text-left text-sm font-medium capitalize text-white shadow-[0_10px_30px_rgba(0,0,0,0.18)] outline-none transition hover:border-cyanGlow/45 hover:bg-[#171b2b] focus-visible:border-cyanGlow/70 focus-visible:ring-2 focus-visible:ring-cyanGlow/20 disabled:cursor-not-allowed disabled:opacity-45"
                      aria-label="Shared difficulty"
                      aria-haspopup="listbox"
                      aria-expanded={difficultyMenuOpen}
                      disabled={isPending}
                      onClick={() => setDifficultyMenuOpen((open) => !open)}
                      onKeyDown={(event) => {
                        if (event.key === "Escape") {
                          setDifficultyMenuOpen(false);
                        }
                        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                          event.preventDefault();
                          const currentIndex = difficultyOptions.indexOf(difficulty);
                          const direction = event.key === "ArrowDown" ? 1 : -1;
                          const nextIndex = (currentIndex + direction + difficultyOptions.length) % difficultyOptions.length;
                          setDifficulty(difficultyOptions[nextIndex]);
                          setDifficultyMenuOpen(true);
                        }
                      }}
                    >
                      <span>{difficulty}</span>
                      <ChevronDown className={`text-white/45 transition-transform ${difficultyMenuOpen ? "rotate-180" : ""}`} size={17} />
                    </button>
                    {difficultyMenuOpen ? (
                      <div
                        className="absolute bottom-full right-0 z-30 mb-2 w-full overflow-hidden rounded-xl border border-white/15 bg-[#151827] p-1 shadow-[0_18px_50px_rgba(0,0,0,0.55)]"
                        role="listbox"
                        aria-label="Shared difficulty options"
                      >
                        {difficultyOptions.map((option) => {
                          const selected = option === difficulty;
                          return (
                            <button
                              key={option}
                              type="button"
                              className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm capitalize outline-none transition ${
                                selected
                                  ? "bg-cyanGlow/15 font-medium text-cyanGlow"
                                  : "text-white/70 hover:bg-white/[0.07] hover:text-white focus-visible:bg-white/[0.07] focus-visible:text-white"
                              }`}
                              role="option"
                              aria-selected={selected}
                              onClick={() => {
                                setDifficulty(option);
                                setDifficultyMenuOpen(false);
                              }}
                            >
                              <span>{option}</span>
                              {selected ? <Check size={16} /> : null}
                            </button>
                          );
                        })}
                      </div>
                    ) : null}
                  </div>
                </div>
              </div>
            </div>
          )}

          <div className="flex flex-wrap items-center gap-3">
            {step === 1 ? (
              <>
                <Link className="btn-secondary" href="/admin/assessments">Back to List</Link>
                <button className="btn-primary ml-auto" type="button" onClick={advanceToConfiguration}>
                  Next: Configure
                  <ArrowRight size={16} />
                </button>
              </>
            ) : (
              <>
                <button
                  className="btn-secondary"
                  type="button"
                  disabled={isPending}
                  onClick={() => {
                    setDifficultyMenuOpen(false);
                    setError(null);
                    setConfigurationReady(false);
                    setStep(1);
                  }}
                >
                  <ArrowLeft size={16} />
                  Previous
                </button>
                <button
                  className="btn-secondary"
                  type="button"
                  disabled={!configurationReady || isPending || totalTasks === 0}
                  onClick={() => void createFromConfiguration("generate")}
                >
                  {pendingMode === "generate" ? <Loader2 className="animate-spin" size={16} /> : <Wand2 size={16} />}
                  {pendingMode === "generate" ? "Generating Questions..." : "Generate Questions"}
                </button>
                <button
                  className="btn-primary"
                  type="button"
                  disabled={!configurationReady || isPending}
                  onClick={() => void createFromConfiguration("manual")}
                >
                  {pendingMode === "manual" ? <Loader2 className="animate-spin" size={16} /> : null}
                  {pendingMode === "manual" ? "Saving in backend..." : "Save assessment"}
                </button>
                <Link className={`btn-secondary ml-auto ${isPending ? "pointer-events-none opacity-45" : ""}`} href="/admin/assessments" aria-disabled={isPending}>Back to List</Link>
              </>
            )}
            {pendingMode ? (
              <span className="w-full text-sm text-white/55" aria-live="polite">
                {pendingMode === "generate"
                  ? `Backend is generating ${totalTasks} validated Todo-prototype task${totalTasks === 1 ? "" : "s"}. Nothing is saved until every draft is confirmed.`
                  : "Saving assessment shell in the backend..."}
              </span>
            ) : null}
            {error ? <span className="w-full text-sm text-pinkGlow">{error}</span> : null}
          </div>
        </form>
      </section>
    </div>
  );
}
