"use client";

import { SemanticIcon, type SemanticIconName } from "@/components/ui/SemanticIcon";
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

function getIcon(taskType?: TaskType): SemanticIconName {
  switch (taskType) {
    case "frontend_ui_extension":
      return "frontend";
    case "rest_api_development":
      return "api";
    case "database_query_schema":
      return "database";
    case "bug_fix":
      return "bug";
    default:
      return "file";
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
      return "Run executes public checks and renders the browser UI preview.";
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

function buildPreviewDocument(sandboxDocument: string) {
  const securityHead = `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; connect-src 'none'; img-src data: blob:; media-src data: blob:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; font-src data:; form-action 'none'; base-uri 'none'; object-src 'none'; frame-src 'none';">`;
  if (/<head[\s>]/i.test(sandboxDocument)) {
    return sandboxDocument.replace(/<head([^>]*)>/i, `<head$1>${securityHead}`);
  }
  return `<!doctype html><html><head>${securityHead}</head><body>${sandboxDocument}</body></html>`;
}

function BrowserPreviewFrame({ runResult }: {
  runResult: RunResult | null;
}) {
  const htmlOutput = runResult?.preview_document ?? null;

  if (!htmlOutput) {
    return (
      <div className="grid h-[170px] place-items-center rounded-xl border border-white/10 bg-slate-950 px-6 text-center xl:h-[190px]">
        <div>
          <p className="text-sm font-semibold text-white">Preview not available yet</p>
          <p className="mt-2 text-xs leading-5 text-white/50">
            Run must return browser HTML before this panel can render a preview.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative h-full min-h-[280px] overflow-hidden rounded-xl border border-white/10 bg-white">
      <span className="absolute right-2 top-2 z-10 rounded border border-emerald-500/25 bg-[#07111d] px-2 py-1 text-[10px] text-emerald-300">
        Live preview
      </span>
      <iframe
        className="h-full min-h-[280px] w-full bg-white"
        sandbox="allow-forms allow-scripts"
        referrerPolicy="no-referrer"
        srcDoc={buildPreviewDocument(htmlOutput)}
        title="Browser UI preview"
      />
    </div>
  );
}

export function TaskVerificationPreview({ question, runResult, runState }: TaskVerificationPreviewProps) {
  const iconName = getIcon(question?.task_type);
  const hasRun = runResult !== null;
  const passed = runResult?.test_results.filter((test) => test.passed).length ?? 0;
  const total = runResult?.test_results.length ?? 0;
  const metadata = getPrimaryMetadata(question);
  const isBrowserPreview = question?.verification_mode === "browser_ui_preview";

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-xl border border-white/10 bg-[#07111d]">
      {!isBrowserPreview ? (
        <div className="flex items-center gap-3 border-b border-white/10 bg-[#0e1726] px-4 py-2.5">
          <span className="grid h-8 w-8 place-items-center rounded-lg border border-white/10 bg-[#07111d] text-cyanGlow">
            <SemanticIcon name={iconName} size={16} />
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
      ) : null}

      <div className={`scrollbar-soft flex-1 overflow-y-auto ${isBrowserPreview ? "p-2" : "p-4"}`}>
        {runState === "running" ? (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-cyanGlow/20 bg-cyanGlow/5 px-3 py-2 text-xs text-cyanGlow">
            <div className="h-2 w-2 animate-pulse rounded-full bg-cyanGlow" />
            Running verification for the selected task...
          </div>
        ) : null}

        {isBrowserPreview ? (
          <BrowserPreviewFrame runResult={runResult} />
        ) : (
          <div className="rounded-xl border border-white/10 bg-[#0e1726] p-4">
            <div className="flex items-start gap-3">
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg border border-cyanGlow/15 bg-[#07111d] text-cyanGlow">
                <SemanticIcon name="file" size={17} />
              </span>
              <div className="min-w-0">
                <p className="font-medium text-white">{question?.title ?? "Selected task"}</p>
                <p className="mt-1 text-sm leading-6 text-white/55">{getVerificationCopy(question)}</p>
                {metadata ? <p className="mt-3 font-mono text-xs text-cyanGlow/80">{metadata}</p> : null}
              </div>
            </div>
          </div>
        )}

        {hasRun && !isBrowserPreview ? (
          <div className="mt-4 space-y-2">
            {runResult.test_results.map((test) => (
              <div key={test.name} className="flex items-start gap-3 rounded-lg border border-white/10 bg-[#0e1726] px-3 py-2 text-xs">
                <SemanticIcon name={test.passed ? "check" : "fail"} size={14} className={test.passed ? "text-emerald-400" : "text-rose-400"} />
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-white/80">{test.name}</p>
                  {test.output ? <p className="mt-1 whitespace-pre-wrap font-mono text-white/45">{test.output}</p> : null}
                </div>
              </div>
            ))}
          </div>
        ) : !hasRun && !isBrowserPreview ? (
          <div className="mt-4 rounded-lg border border-dashed border-white/10 bg-[#0e1726] px-3 py-6 text-center text-sm text-white/35">
            Run the selected task to populate this verification area. Hidden tests remain private.
          </div>
        ) : null}
      </div>
    </div>
  );
}
