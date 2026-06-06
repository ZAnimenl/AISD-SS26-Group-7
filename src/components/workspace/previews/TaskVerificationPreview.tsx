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
      return "Run executes public checks and renders the browser UI preview returned by the sandbox.";
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

function getHtmlOutput(runResult: RunResult | null) {
  return runResult?.test_results.find((test) => /<\/?[a-z][\s\S]*>/i.test(test.output))?.output ?? null;
}

function buildPreviewDocument(bodyHtml: string) {
  return `<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline';" />
    <style>
      :root { color-scheme: dark; font-family: Barlow, Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
      * { box-sizing: border-box; }
      body {
        margin: 0;
        min-height: 100vh;
        color: #eef7ff;
        background:
          radial-gradient(circle at 18% 10%, rgba(0, 229, 255, 0.22), transparent 34%),
          radial-gradient(circle at 88% 70%, rgba(168, 85, 247, 0.24), transparent 38%),
          #07111d;
      }
      main { min-height: 100vh; padding: 10px; }
      .prototype-shell {
        width: min(100%, 760px);
        overflow: hidden;
        border: 1px solid rgba(0, 229, 255, 0.24);
        background: rgba(8, 15, 28, 0.82);
        box-shadow: 0 18px 48px rgba(0, 0, 0, 0.38), inset 0 1px 0 rgba(255,255,255,0.08);
      }
      .canvas { padding: 12px; }
      section[data-testid="todo-summary"] {
        border: 1px solid rgba(255,255,255,0.10);
        background: linear-gradient(145deg, rgba(255,255,255,0.08), rgba(0,229,255,0.05));
        padding: 14px;
        box-shadow: inset 0 1px 0 rgba(255,255,255,0.08);
      }
      h2 { margin: 0 0 10px; font-size: 21px; line-height: 1.2; color: #ffffff; letter-spacing: 0; }
      p { margin: 6px 0; color: rgba(238,247,255,0.72); }
      .eyebrow { margin: 0 0 8px; color: #7eeaff; font-size: 12px; text-transform: uppercase; letter-spacing: 0.12em; }
      .metrics { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; margin: 12px 0; }
      article { border: 1px solid rgba(255,255,255,0.10); background: rgba(255,255,255,0.06); padding: 10px; }
      strong { display: block; color: #ffffff; font-size: 24px; line-height: 1; text-shadow: 0 0 18px rgba(0,229,255,0.22); }
      span { color: rgba(238,247,255,0.55); font-size: 12px; }
      .status {
        border-left: 3px solid #34d399;
        background: rgba(16,185,129,0.10);
        padding: 10px 12px;
        color: rgba(238,247,255,0.76);
      }
    </style>
  </head>
  <body>
    <main>
      <div class="prototype-shell">
        <div class="canvas">${bodyHtml}</div>
      </div>
    </main>
  </body>
</html>`;
}

function BrowserPreviewFrame({ runResult }: {
  runResult: RunResult | null;
}) {
  const htmlOutput = getHtmlOutput(runResult);

  if (!htmlOutput) {
    return (
      <div className="grid h-[170px] place-items-center rounded-xl border border-white/10 bg-slate-950 px-6 text-center xl:h-[190px]">
        <div>
          <p className="text-sm font-semibold text-white">No sandbox preview output</p>
          <p className="mt-2 text-xs leading-5 text-white/50">
            Run must return browser HTML before this panel can render a preview.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative overflow-hidden rounded-xl border border-white/10 bg-slate-950">
      <span className="absolute right-2 top-2 z-10 rounded border border-emerald-500/25 bg-black/55 px-2 py-1 text-[10px] text-emerald-300 backdrop-blur">
        Sandbox output
      </span>
      <iframe
        className="h-[170px] w-full bg-[#07111d] xl:h-[190px]"
        sandbox=""
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
    <div className="flex h-full flex-col overflow-hidden rounded-xl border border-white/10 bg-[#0a0f1a]">
      {!isBrowserPreview ? (
        <div className="flex items-center gap-3 border-b border-white/10 bg-white/[0.03] px-4 py-2.5">
          <span className="grid h-8 w-8 place-items-center rounded-lg border border-white/10 bg-black/20 text-cyanGlow">
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
          <>
            <BrowserPreviewFrame
              runResult={runResult}
            />
            <div className="mt-3 rounded-xl border border-white/10 bg-white/[0.03] p-3">
              <div className="flex items-start gap-3">
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-cyanGlow/10 text-cyanGlow">
                  <SemanticIcon name="file" size={16} />
                </span>
                <div className="min-w-0">
                  <p className="font-medium text-white">{question?.title ?? "Selected task"}</p>
                  <p className="mt-1 text-xs leading-5 text-white/50">{getVerificationCopy(question)}</p>
                  {metadata ? <p className="mt-2 font-mono text-xs text-cyanGlow/80">{metadata}</p> : null}
                </div>
              </div>
            </div>
          </>
        ) : (
          <div className="rounded-xl border border-white/10 bg-white/[0.03] p-4">
            <div className="flex items-start gap-3">
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-cyanGlow/10 text-cyanGlow">
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
              <div key={test.name} className="flex items-start gap-3 rounded-lg border border-white/10 bg-black/25 px-3 py-2 text-xs">
                <SemanticIcon name={test.passed ? "check" : "fail"} size={14} className={test.passed ? "text-emerald-400" : "text-rose-400"} />
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-white/80">{test.name}</p>
                  {test.output ? <p className="mt-1 whitespace-pre-wrap font-mono text-white/45">{test.output}</p> : null}
                </div>
              </div>
            ))}
          </div>
        ) : !hasRun && !isBrowserPreview ? (
          <div className="mt-4 rounded-lg border border-dashed border-white/10 px-3 py-6 text-center text-sm text-white/35">
            Run the selected task to populate this verification area. Hidden tests remain private.
          </div>
        ) : null}
      </div>
    </div>
  );
}
