"use client";

import { CheckCircle2, Database, Eye, FileText, Server, TestTube2, Wrench, XCircle } from "lucide-react";
import type { Question, RunResult, TaskType, VerificationMode } from "@/lib/types";

interface TaskVerificationPreviewProps {
  question: Question | undefined;
  runResult: RunResult | null;
  runState: "idle" | "running";
}

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

function getIcon(taskType?: TaskType) {
  switch (taskType) {
    case "frontend_ui_extension":
      return Eye;
    case "rest_api_development":
      return Server;
    case "database_query_schema":
      return Database;
    case "bug_fix":
      return Wrench;
    default:
      return TestTube2;
  }
}

function getTaskLabel(taskType?: TaskType) {
  return taskType ? TASK_LABELS[taskType] : "Practical task";
}

function getVerificationLabel(mode?: VerificationMode) {
  return mode ? VERIFICATION_LABELS[mode] : "Automated check";
}

function getVerificationCopy(question: Question | undefined) {
  switch (question?.verification_mode) {
    case "browser_ui_preview":
      return "Run will execute public checks and reserve this area for the browser UI preview of the selected task.";
    case "api_response_check":
      return "Run will verify the selected route handler and show public request/response test output.";
    case "database_result_check":
      return "Run will verify query or repository behavior and show public database-oriented result checks.";
    case "regression_test":
      return "Run will execute public regression checks for the selected defect.";
    case "automated_test":
      return "Run will execute public automated tests for the selected task.";
    default:
      return "Run will show public verification output for the selected task.";
  }
}

function getPrimaryMetadata(question: Question | undefined) {
  const metadata = question?.verification_metadata ?? {};
  return metadata.endpoint ?? metadata.result_shape ?? metadata.focus ?? metadata.preview_entry ?? metadata.primary_view ?? null;
}

function StatusIcon({ passed }: { passed: boolean }) {
  return passed ? <CheckCircle2 size={14} className="text-emerald-400" /> : <XCircle size={14} className="text-rose-400" />;
}

export function TaskVerificationPreview({ question, runResult, runState }: TaskVerificationPreviewProps) {
  const Icon = getIcon(question?.task_type);
  const hasRun = runResult !== null;
  const passed = runResult?.test_results.filter((test) => test.passed).length ?? 0;
  const total = runResult?.test_results.length ?? 0;
  const metadata = getPrimaryMetadata(question);

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-xl border border-white/10 bg-[#0a0f1a]">
      <div className="flex items-center gap-3 border-b border-white/10 bg-white/[0.03] px-4 py-2.5">
        <span className="grid h-8 w-8 place-items-center rounded-lg border border-white/10 bg-black/20 text-cyanGlow">
          <Icon size={16} />
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-white">{getTaskLabel(question?.task_type)}</p>
          <p className="truncate text-xs text-white/40">{getVerificationLabel(question?.verification_mode)}</p>
        </div>
        {hasRun ? (
          <span className={`ml-auto rounded border px-2 py-1 text-xs ${
            passed === total
              ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-300"
              : "border-amber-500/30 bg-amber-500/10 text-amber-300"
          }`}>
            {passed}/{total} public checks
          </span>
        ) : null}
      </div>

      <div className="scrollbar-soft flex-1 overflow-y-auto p-4">
        {runState === "running" ? (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-cyanGlow/20 bg-cyanGlow/5 px-3 py-2 text-xs text-cyanGlow">
            <div className="h-2 w-2 animate-pulse rounded-full bg-cyanGlow" />
            Running verification for the selected task...
          </div>
        ) : null}

        <div className="rounded-xl border border-white/10 bg-white/[0.03] p-4">
          <div className="flex items-start gap-3">
            <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-cyanGlow/10 text-cyanGlow">
              <FileText size={17} />
            </span>
            <div className="min-w-0">
              <p className="font-medium text-white">{question?.title ?? "Selected task"}</p>
              <p className="mt-1 text-sm leading-6 text-white/55">{getVerificationCopy(question)}</p>
              {metadata ? <p className="mt-3 font-mono text-xs text-cyanGlow/80">{metadata}</p> : null}
            </div>
          </div>
        </div>

        {hasRun ? (
          <div className="mt-4 space-y-2">
            {runResult.test_results.map((test) => (
              <div key={test.name} className="flex items-start gap-3 rounded-lg border border-white/10 bg-black/25 px-3 py-2 text-xs">
                <StatusIcon passed={test.passed} />
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-white/80">{test.name}</p>
                  {test.output ? <p className="mt-1 whitespace-pre-wrap font-mono text-white/45">{test.output}</p> : null}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="mt-4 rounded-lg border border-dashed border-white/10 px-3 py-6 text-center text-sm text-white/35">
            Run the selected task to populate this verification area. Hidden tests remain private.
          </div>
        )}
      </div>
    </div>
  );
}
