"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, Check, Clock3, FileText, ListChecks, Loader2, Minus, Plus, Wand2 } from "lucide-react";
import { generateAssessment, getAdminAssessment, updateAssessment } from "@/lib/api";
import { currentUtcIso, defaultAssessmentExpiry, toLocalDateTimeInput, toUtcIso } from "@/lib/assessmentSchedule";
import { QuestionTestCaseEditor } from "@/components/admin/QuestionTestCaseEditor";
import { DurationSlider } from "@/components/admin/DurationSlider";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { CustomDropdown } from "@/components/ui/CustomDropdown";
import type { Assessment, AssessmentStatus, Difficulty, TaskType } from "@/lib/types";

type CreateStep = 1 | 2 | 3;
type PendingAction = "generate" | "save" | null;

const MAX_TASKS_PER_TYPE = 5;
const MAX_TOTAL_TASKS = 12;
const difficultyOptions: Difficulty[] = ["easy", "medium", "hard"];
const taskBlueprints: Array<{ type: TaskType; label: string; description: string }> = [
  { type: "frontend_ui_extension", label: "Frontend UI extension", description: "Browser UI and interaction work" },
  { type: "rest_api_development", label: "REST API development", description: "Todo endpoints and service logic" },
  { type: "database_query_schema", label: "Database query/schema", description: "Schema, migration, and reporting work" },
  { type: "bug_fix", label: "Bug fix", description: "Cross-file diagnosis and regression repair" }
];

const steps = [
  { number: 1 as const, label: "Assessment basics", icon: FileText },
  { number: 2 as const, label: "Generate & review", icon: ListChecks },
  { number: 3 as const, label: "Delivery settings", icon: Clock3 }
];

export default function NewAssessmentPage() {
  const router = useRouter();
  const titleRef = useRef<HTMLInputElement>(null);
  const descriptionRef = useRef<HTMLTextAreaElement>(null);
  const reviewSectionRef = useRef<HTMLDivElement>(null);
  const [step, setStep] = useState<CreateStep>(1);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [draftAssessment, setDraftAssessment] = useState<Assessment | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const [shouldScrollToReview, setShouldScrollToReview] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [difficulty, setDifficulty] = useState<Difficulty>("medium");
  const [taskCounts, setTaskCounts] = useState<Record<TaskType, number>>({
    frontend_ui_extension: 1,
    rest_api_development: 1,
    database_query_schema: 1,
    bug_fix: 1
  });
  const [durationMinutes, setDurationMinutes] = useState(50);
  const [startMode, setStartMode] = useState<"now" | "scheduled">("now");
  const [scheduledStart, setScheduledStart] = useState(() => toLocalDateTimeInput());
  const [expiresAt, setExpiresAt] = useState(defaultAssessmentExpiry);
  const [assessmentStatus, setAssessmentStatus] = useState<AssessmentStatus>("draft");
  const [aiAccess, setAiAccess] = useState<"enabled" | "disabled">("enabled");
  const isPending = pendingAction !== null;
  const totalTasks = Object.values(taskCounts).reduce((sum, count) => sum + count, 0);
  const isBlueprintFrozen = isPending || Boolean(draftAssessment);

  useEffect(() => {
    if (!shouldScrollToReview || !draftAssessment?.questions.length) return;

    window.requestAnimationFrame(() => {
      reviewSectionRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
      setShouldScrollToReview(false);
    });
  }, [draftAssessment, shouldScrollToReview]);

  function updateTaskCount(taskType: TaskType, requestedCount: number) {
    setTaskCounts((current) => {
      const currentTotal = Object.values(current).reduce((sum, count) => sum + count, 0);
      const maximumForType = Math.min(MAX_TASKS_PER_TYPE, current[taskType] + (MAX_TOTAL_TASKS - currentTotal));
      return { ...current, [taskType]: Math.max(0, Math.min(maximumForType, requestedCount)) };
    });
  }

  function advanceFromBasics() {
    if (!titleRef.current?.reportValidity() || !descriptionRef.current?.reportValidity()) return;
    setError(null);
    setStep(2);
  }

  async function createDraft() {
    if (isPending || draftAssessment) return;
    setError(null);
    setPendingAction("generate");
    try {
      const provisionalStart = currentUtcIso();
      const provisionalExpiry = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
      const created = await generateAssessment({
        title: title.trim(),
        description: description.trim(),
        duration_minutes: durationMinutes,
        starts_at: provisionalStart,
        expires_at: provisionalExpiry,
        status: "draft",
        ai_enabled: aiAccess === "enabled",
        shared_prototype_reference: "default-todo-list",
        shared_prototype_version: "1.0",
        task_type_counts: taskCounts,
        difficulty
      });
      setDraftAssessment(await getAdminAssessment(created.assessment_id));
      setShouldScrollToReview(true);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to create the assessment draft.");
    } finally {
      setPendingAction(null);
    }
  }

  function advanceToDelivery() {
    if (!draftAssessment || draftAssessment.questions.length === 0) {
      setError("Generate or add at least one question before configuring delivery.");
      return;
    }
    setError(null);
    setStep(3);
  }

  async function saveDeliverySettings() {
    if (!draftAssessment || isPending) return;
    if (!Number.isInteger(durationMinutes) || durationMinutes <= 0) {
      setError("Duration must be a whole number greater than zero.");
      return;
    }
    const startsAt = startMode === "scheduled" ? toUtcIso(scheduledStart) : currentUtcIso();
    const expiry = toUtcIso(expiresAt);
    if (!expiry || new Date(expiry).getTime() <= new Date(startsAt ?? currentUtcIso()).getTime()) {
      setError("Assessment expiration must be later than its start time.");
      return;
    }

    setError(null);
    setPendingAction("save");
    try {
      const completedAssessment: Assessment = {
        ...draftAssessment,
        title: title.trim(),
        description: description.trim(),
        duration_minutes: durationMinutes,
        starts_at: startsAt,
        expires_at: expiry,
        status: assessmentStatus,
        ai_enabled: aiAccess === "enabled"
      };
      await updateAssessment(completedAssessment);
      router.push("/admin/assessments");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to save delivery settings.");
    } finally {
      setPendingAction(null);
    }
  }

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Create assessment" />
      <section className="panel">
        <div className="relative">
          <ol className="grid gap-2 md:grid-cols-3" aria-label="Assessment creation progress">
            {steps.map(({ number, label, icon: Icon }) => {
              const complete = step > number;
              const active = step === number;
              const basicsComplete = Boolean(title.trim() && description.trim());
              const canOpen = number === 1 || (number === 2 && basicsComplete) || (number === 3 && Boolean(draftAssessment?.questions.length));
              return (
                <li key={number}>
                  <button
                    type="button"
                    disabled={!canOpen || isPending}
                    className={`flex w-full items-center gap-3 rounded-xl border px-4 py-3 text-left transition ${
                      active
                        ? "border-cyanGlow/45 bg-cyanGlow/10 text-white"
                        : complete
                          ? "border-cyanGlow/20 bg-cyanGlow/[0.05] text-white/70"
                          : "border-white/10 bg-black/20 text-white/40"
                    } disabled:cursor-not-allowed disabled:opacity-45`}
                    aria-current={active ? "step" : undefined}
                    onClick={() => setStep(number)}
                  >
                    <span className={`grid h-10 w-10 shrink-0 place-items-center rounded-full border ${
                      active || complete ? "border-cyanGlow/45 bg-cyanGlow/10 text-cyanGlow" : "border-white/10 bg-white/5"
                    }`}>
                      {complete ? <Check size={17} /> : <Icon size={17} />}
                    </span>
                    <span>
                      <span className="block text-[10px] uppercase tracking-[0.14em] text-white/35">Step {number}</span>
                      <span className="block text-sm font-medium">{label}</span>
                    </span>
                  </button>
                </li>
              );
            })}
          </ol>

          {step === 1 ? (
            <div className="mt-6 grid gap-6 rounded-2xl border border-white/10 bg-black/15 p-5 sm:p-6">
              <div>
                <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Assessment basics</p>
                <h2 className="mt-1 text-xl font-semibold">Define the assessment</h2>
                <p className="mt-1 text-sm text-white/40">Start with the student-facing title and context.</p>
              </div>
              <label className="grid gap-2 text-sm text-white/60">
                Title
                <input ref={titleRef} className="field" value={title} required autoFocus onChange={(event) => setTitle(event.target.value)} />
              </label>
              <label className="grid gap-2 text-sm text-white/60">
                Description
                <textarea ref={descriptionRef} className="field min-h-36" value={description} required onChange={(event) => setDescription(event.target.value)} />
              </label>
              <div className="flex flex-wrap gap-3">
                <Link className="btn-secondary" href="/admin/assessments">Back to list</Link>
                <button className="btn-primary ml-auto" type="button" onClick={advanceFromBasics}>
                  Next: Generate questions <ArrowRight size={16} />
                </button>
              </div>
            </div>
          ) : null}

          {step === 2 ? (
            <div className="mt-6 space-y-5">
              <div className="rounded-2xl border border-white/10 bg-black/20 p-5">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Question blueprint</p>
                    <h2 className="mt-1 text-xl font-semibold">Generate the task set</h2>
                    <p className="mt-1 text-sm text-white/45">
                      {draftAssessment
                        ? "Blueprint locked. Review every generated task and test before scheduling."
                        : "Choose the mix, generate the questions, then review every task and test before scheduling."}
                    </p>
                  </div>
                  <div className="grid min-w-28 place-items-center rounded-xl border border-cyanGlow/25 bg-cyanGlow/10 px-4 py-2">
                    <span className="text-[10px] uppercase tracking-[0.14em] text-white/40">Total tasks</span>
                    <span className="text-2xl font-semibold text-cyanGlow">{totalTasks}</span>
                  </div>
                </div>
                <div className="mt-5 grid gap-3 lg:grid-cols-2">
                  {taskBlueprints.map((blueprint) => {
                    const count = taskCounts[blueprint.type];
                    return (
                      <div key={blueprint.type} className="rounded-xl border border-white/10 bg-white/[0.035] p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div><p className="font-medium text-white/85">{blueprint.label}</p><p className="mt-1 text-xs text-white/40">{blueprint.description}</p></div>
                          <span className="grid h-9 min-w-9 place-items-center rounded-lg border border-cyanGlow/25 bg-cyanGlow/10 font-mono text-sm text-cyanGlow">{count}</span>
                        </div>
                        <div className="mt-4 grid grid-cols-[36px_1fr_36px] items-center gap-3">
                          <button
                            className="grid h-9 w-9 place-items-center rounded-full border border-white/15 text-white/65 disabled:cursor-not-allowed disabled:opacity-30"
                            type="button"
                            aria-label={`Decrease ${blueprint.label} question count`}
                            disabled={isBlueprintFrozen || count === 0}
                            onClick={() => updateTaskCount(blueprint.type, count - 1)}
                          >
                            <Minus size={15} />
                          </button>
                          <input
                            className="h-2 w-full accent-cyan-400 disabled:cursor-not-allowed disabled:opacity-45"
                            type="range"
                            min={0}
                            max={MAX_TASKS_PER_TYPE}
                            value={count}
                            aria-label={`${blueprint.label} question count`}
                            disabled={isBlueprintFrozen}
                            onChange={(event) => updateTaskCount(blueprint.type, Number(event.target.value))}
                          />
                          <button
                            className="grid h-9 w-9 place-items-center rounded-full border border-white/15 text-white/65 disabled:cursor-not-allowed disabled:opacity-30"
                            type="button"
                            aria-label={`Increase ${blueprint.label} question count`}
                            disabled={isBlueprintFrozen || totalTasks >= MAX_TOTAL_TASKS || count >= MAX_TASKS_PER_TYPE}
                            onClick={() => updateTaskCount(blueprint.type, count + 1)}
                          >
                            <Plus size={15} />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
                <div className="mt-4 grid gap-4 rounded-xl border border-white/10 bg-black/20 p-4 sm:grid-cols-[1fr_180px] sm:items-center">
                  <div><p className="text-sm font-medium text-white/80">Shared difficulty</p><p className="mt-1 text-xs text-white/40">Applied to each generated task; individual tasks remain editable afterward.</p></div>
                  <CustomDropdown ariaLabel="Shared difficulty" value={difficulty} disabled={isBlueprintFrozen} options={difficultyOptions.map((value) => ({ value, label: value }))} onChange={setDifficulty} />
                </div>
                {!draftAssessment ? (
                  <div className="mt-5 flex flex-wrap gap-3">
                    <button className="btn-secondary" type="button" disabled={isPending} onClick={() => setStep(1)}><ArrowLeft size={16} /> Previous</button>
                    <button className="btn-primary ml-auto" type="button" disabled={isPending || totalTasks === 0} onClick={() => void createDraft()}>
                      {pendingAction === "generate" ? <Loader2 className="animate-spin" size={16} /> : <Wand2 size={16} />}
                      {pendingAction === "generate" ? "Generating questions..." : "Generate questions"}
                    </button>
                  </div>
                ) : null}
              </div>
              {draftAssessment ? (
                <>
                  <div ref={reviewSectionRef} className="scroll-mt-6 rounded-2xl border border-cyanGlow/25 bg-cyanGlow/[0.06] p-4">
                    <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Review checkpoint</p>
                    <h2 className="mt-1 text-lg font-semibold">Review every generated task and test</h2>
                    <p className="mt-1 text-sm text-white/50">Save any edits below. Delivery settings remain separate in the final step.</p>
                  </div>
                  <QuestionTestCaseEditor assessment={draftAssessment} onAssessmentChange={setDraftAssessment} />
                  <div className="flex flex-wrap gap-3">
                    <button className="btn-secondary" type="button" disabled={isPending} onClick={() => setStep(1)}><ArrowLeft size={16} /> Previous</button>
                    <button className="btn-primary ml-auto" type="button" disabled={isPending || draftAssessment.questions.length === 0} onClick={advanceToDelivery}>
                      Next: Delivery settings <ArrowRight size={16} />
                    </button>
                  </div>
                </>
              ) : null}
            </div>
          ) : null}

          {step === 3 ? (
            <div className="mt-6 grid gap-5 rounded-2xl border border-white/10 bg-black/15 p-5">
              <div>
                <p className="text-xs uppercase tracking-[0.14em] text-cyanGlow/70">Delivery settings</p>
                <h2 className="mt-1 text-xl font-semibold">Choose when students can work</h2>
                <p className="mt-1 text-sm text-white/40">Finalize duration, availability, expiry, publication status, and AI access.</p>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <DurationSlider value={durationMinutes} onChange={setDurationMinutes} disabled={isPending} />
                <div className="grid gap-2 text-sm text-white/60">
                  Starts
                  <div className="grid grid-cols-2 rounded-xl border border-white/10 bg-black/20 p-1">
                    {(["now", "scheduled"] as const).map((mode) => (
                      <button key={mode} type="button" className={`rounded-lg px-3 py-2 text-sm font-medium ${startMode === mode ? "bg-cyanGlow/15 text-cyanGlow" : "text-white/45"}`} aria-pressed={startMode === mode} onClick={() => setStartMode(mode)}>
                        {mode === "now" ? "Now" : "Schedule"}
                      </button>
                    ))}
                  </div>
                </div>
                {startMode === "scheduled" ? (
                  <label className="grid gap-2 text-sm text-white/60 sm:col-span-2">
                    Start date and time
                    <input className="field [color-scheme:dark]" type="datetime-local" value={scheduledStart} min={toLocalDateTimeInput(new Date().toISOString())} required onChange={(event) => setScheduledStart(event.target.value)} />
                  </label>
                ) : null}
                <label className="grid gap-2 text-sm text-white/60 sm:col-span-2">
                  Assessment expires
                  <input className="field [color-scheme:dark]" type="datetime-local" value={expiresAt} required onChange={(event) => setExpiresAt(event.target.value)} />
                  <span className="text-xs text-white/35">After this deadline students can review results, but cannot start or continue.</span>
                </label>
                <label className="grid gap-2 text-sm text-white/60">
                  Status
                  <CustomDropdown ariaLabel="Status" value={assessmentStatus} onChange={setAssessmentStatus} options={["draft", "active", "closed", "archived"].map((value) => ({ value: value as AssessmentStatus, label: value }))} />
                </label>
                <label className="grid gap-2 text-sm text-white/60">
                  AI assistance
                  <CustomDropdown ariaLabel="AI assistance" value={aiAccess} onChange={setAiAccess} options={[{ value: "enabled", label: "enabled" }, { value: "disabled", label: "disabled" }]} />
                </label>
              </div>
              <div className="flex flex-wrap gap-3">
                <button className="btn-secondary" type="button" disabled={isPending} onClick={() => setStep(2)}><ArrowLeft size={16} /> Review questions</button>
                <button className="btn-primary ml-auto" type="button" disabled={isPending} onClick={() => void saveDeliverySettings()}>
                  {pendingAction === "save" ? <Loader2 className="animate-spin" size={16} /> : null}
                  {pendingAction === "save" ? "Saving assessment..." : "Finish assessment"}
                </button>
              </div>
            </div>
          ) : null}

          {error ? <p className="mt-4 text-sm text-pinkGlow" role="alert">{error}</p> : null}
        </div>
      </section>
    </div>
  );
}
