import { SemanticIcon } from "@/components/ui/SemanticIcon";
import type { RunResult } from "@/lib/types";

export function formatExecutionStatus(status: RunResult["status"]) {
  return status.replaceAll("_", " ");
}

function hasRuntimeErrorMarker(stderr: string | null) {
  if (!stderr) {
    return false;
  }

  const markers = [
    "Traceback (most recent call last)",
    "SyntaxError",
    "TypeError",
    "NameError",
    "ModuleNotFoundError",
    "ImportError",
    "IndentationError",
    "ReferenceError",
    "Cannot find module"
  ];

  return markers.some((marker) => stderr.toLowerCase().includes(marker.toLowerCase()));
}

function isRunEnvironmentUnavailable(stderr: string | null) {
  return Boolean(stderr?.includes("Run environment unavailable") || stderr?.includes("Grader container unavailable"));
}

export function getDisplayStatus(runResult: RunResult) {
  if (isRunEnvironmentUnavailable(runResult.stderr)) {
    return "internal_error" as RunResult["status"];
  }

  if (
    runResult.status === "runtime_error"
    && runResult.test_results.length > 0
    && runResult.stdout.toLowerCase().includes("tests passed")
    && !hasRuntimeErrorMarker(runResult.stderr)
  ) {
    return "failed" as RunResult["status"];
  }

  return runResult.status;
}

export function getStatusClass(status: RunResult["status"]) {
  switch (status) {
    case "passed":
      return "border-emerald-500/30 bg-emerald-500/10 text-emerald-300";
    case "failed":
      return "border-amber-500/30 bg-amber-500/10 text-amber-300";
    case "time_limit_exceeded":
    case "memory_limit_exceeded":
    case "runtime_error":
    case "internal_error":
      return "border-rose-500/30 bg-rose-500/10 text-rose-300";
    default:
      return "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow";
  }
}

export function ConsolePanel({ isRunning, runResult, taskError }: {
  isRunning: boolean;
  runResult: RunResult | null;
  taskError: string | null;
}) {
  if (isRunning) {
    return (
      <div className="flex h-full min-h-0 flex-col overflow-hidden rounded-xl border border-cyanGlow/20 bg-[#07111d] p-4 text-sm text-cyanGlow">
        <div className="flex items-center gap-2">
          <span className="h-2 w-2 animate-pulse rounded-full bg-cyanGlow" />
          Running public checks...
        </div>
      </div>
    );
  }

  if (taskError) {
    return (
      <div className="flex h-full min-h-0 flex-col overflow-hidden rounded-xl border border-rose-500/20 bg-[#07111d] text-sm text-rose-200">
        <div className="shrink-0 border-b border-rose-500/15 bg-[#1c1020] px-4 py-3">
          <p className="font-semibold">Run request failed</p>
        </div>
        <div className="scrollbar-soft min-h-0 flex-1 overflow-y-auto p-4">
          <p className="whitespace-pre-wrap text-rose-200/75">{taskError}</p>
        </div>
      </div>
    );
  }

  if (!runResult) {
    return (
      <div className="grid h-full min-h-0 place-items-center rounded-xl border border-dashed border-white/10 bg-[#07111d] p-4 text-center text-sm text-white/35">
        Run this task to see public test status, safe output, and resource metrics. Hidden tests stay private.
      </div>
    );
  }

  const isEnvironmentUnavailable = isRunEnvironmentUnavailable(runResult.stderr);

  return (
    <div className="flex h-full min-h-0 flex-col overflow-hidden rounded-xl border border-white/10 bg-[#07111d] text-sm text-white/70">
      <div className="scrollbar-soft min-h-0 flex-1 overflow-y-auto overscroll-contain p-3 pr-2">
        <div className="grid gap-2 pb-3">
          {isEnvironmentUnavailable ? (
            <section className="rounded-lg border border-amber-500/25 bg-[#241d0d]">
              <div className="border-b border-amber-500/15 bg-[#2a220f] px-3 py-2 text-xs font-semibold text-amber-200/90">Run environment unavailable</div>
              <p className="p-3 text-xs leading-5 text-amber-100/75">
                The sandbox grader is not reachable right now. This is an environment issue, not a code stderr output.
              </p>
            </section>
          ) : null}

          {runResult.test_results.map((test) => (
            <div key={test.name} className="rounded-lg border border-white/10 bg-[#0e1726] px-3 py-2">
              <div className="flex items-center gap-2">
                <SemanticIcon name={test.passed ? "check" : "fail"} size={14} className={test.passed ? "text-emerald-300" : "text-rose-300"} />
                <span className="min-w-0 flex-1 truncate font-medium text-white/80">{test.name}</span>
                <span className={`shrink-0 rounded border px-1.5 py-0.5 text-[10px] ${test.passed ? "border-emerald-500/20 text-emerald-300" : "border-rose-500/20 text-rose-300"}`}>
                  {test.passed ? "PASS" : "FAIL"}
                </span>
              </div>
              {test.output ? (
                <pre className="mt-2 max-h-40 overflow-y-auto whitespace-pre-wrap rounded-md bg-[#050914] p-2 font-mono text-xs text-white/55">
                  {test.output}
                </pre>
              ) : null}
            </div>
          ))}

          {runResult.stdout ? (
            <section className="rounded-lg border border-white/10 bg-[#0e1726]">
              <div className="border-b border-white/10 bg-[#101a2a] px-3 py-2 text-xs font-medium text-white/55">stdout</div>
              <pre className="max-h-40 overflow-y-auto whitespace-pre-wrap p-3 font-mono text-xs text-white/55">{runResult.stdout}</pre>
            </section>
          ) : null}

          {runResult.stderr && !isEnvironmentUnavailable ? (
            <section className="rounded-lg border border-rose-500/20 bg-[#21111d]">
              <div className="border-b border-rose-500/15 bg-[#261421] px-3 py-2 text-xs font-medium text-rose-200/80">stderr</div>
              <pre className="max-h-40 overflow-y-auto whitespace-pre-wrap p-3 font-mono text-xs text-rose-200/80">{runResult.stderr}</pre>
            </section>
          ) : null}

          <section className="rounded-lg border border-white/10 bg-[#0e1726]">
            <div className="border-b border-white/10 bg-[#101a2a] px-3 py-2 text-xs font-medium text-white/55">metrics</div>
            <div className="grid grid-cols-2 gap-3 p-3 font-mono text-xs text-white/55">
              <span>CPU {runResult.metrics.cpu_time_seconds}s</span>
              <span>Peak memory {runResult.metrics.peak_memory_kb} KB</span>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
