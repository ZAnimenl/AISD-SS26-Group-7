import { BrainCircuit, MessageSquareText, Sparkles } from "lucide-react";
import type { AiGradingStatus } from "@/lib/types";

interface AiAssessmentSummaryProps {
  status: AiGradingStatus;
  summary?: string | null;
  reflectionText?: string | null;
  details?: Record<string, unknown>;
  interactionCount?: number;
  totalTokens?: number;
  confidence?: string | null;
}

function readableConsistency(value: unknown, hasReflection: boolean, summary?: string | null) {
  if (!hasReflection) return "No reflection submitted";

  const rawValue = typeof value === "string" ? value.trim() : "";
  const normalized = rawValue.toLowerCase();
  if (/(strong|consistent|aligned|demonstrat|understand)/.test(normalized) && !/(inconsistent|misaligned|does not)/.test(normalized)) {
    return "Reflection aligns with the recorded work";
  }
  if (/(inconsistent|misaligned|mismatch|does not)/.test(normalized)) {
    return "Reflection needs stronger connection to the recorded work";
  }
  if (normalized && !/(not.?assessed|unknown|pending)/.test(normalized)) {
    return rawValue.replaceAll("_", " ");
  }

  const normalizedSummary = summary?.toLowerCase() ?? "";
  if (/(reflection).*(does not|did not|mismatch|placeholder|inconsistent|unrelated)|does not match the logs/.test(normalizedSummary)) {
    return "Reflection needs stronger connection to the recorded work";
  }
  if (/(reflection).*(align|consistent|demonstrat|understand|verified)/.test(normalizedSummary)) {
    return "Reflection aligns with the recorded work";
  }
  return "Understanding not yet assessed";
}

function statusCopy(status: AiGradingStatus) {
  if (status === "failed") return "The AI-use analysis could not be completed. The functional result remains available.";
  if (status === "reflection_pending") return "AI-use analysis will begin after the reflection is submitted.";
  if (status === "pending") return "AI-use and reflection evidence are being analyzed.";
  return "AI assistance was not enabled for this attempt.";
}

export function AiAssessmentSummary({
  status,
  summary,
  reflectionText,
  details = {},
  interactionCount,
  totalTokens,
  confidence
}: AiAssessmentSummaryProps) {
  const completedSummary = summary?.trim();
  const hasReflection = Boolean(reflectionText?.trim());
  const reflectionAssessment = readableConsistency(details.reflection_consistency, hasReflection, completedSummary);

  return (
    <section className="relative mt-5 overflow-hidden rounded-2xl border border-purpleGlow/25 bg-gradient-to-br from-purpleGlow/10 via-black/20 to-cyanGlow/[0.07] p-5">
      <div className="absolute -right-10 -top-12 h-36 w-36 rounded-full bg-purpleGlow/10 blur-3xl" />
      <div className="relative flex items-start gap-3">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl border border-purpleGlow/30 bg-purpleGlow/10 text-purpleGlow">
          <BrainCircuit size={21} />
        </span>
        <div>
          <p className="text-xs uppercase tracking-[0.14em] text-purpleGlow/80">Assessment insight</p>
          <h3 className="mt-1 text-lg font-semibold">AI use and demonstrated understanding</h3>
          <p className="mt-2 max-w-4xl leading-7 text-white/65">
            {status === "completed"
              ? completedSummary || "The grading evidence was processed, but no narrative summary was returned."
              : statusCopy(status)}
          </p>
        </div>
      </div>
      <div className="relative mt-4 grid gap-3 md:grid-cols-2">
        <div className="rounded-xl border border-white/10 bg-black/20 p-4">
          <div className="flex items-center gap-2 text-sm font-medium text-white/80">
            <Sparkles size={16} className="text-purpleGlow" /> AI assistance
          </div>
          <p className="mt-2 text-sm leading-6 text-white/55">
            {interactionCount != null
              ? `${interactionCount} recorded interaction${interactionCount === 1 ? "" : "s"}${totalTokens != null ? ` using ${totalTokens.toLocaleString()} tokens` : ""}.`
              : status === "completed" ? "Usage patterns are summarized above from the recorded assessment evidence." : "Usage evidence is not available yet."}
          </p>
          {confidence ? <p className="mt-2 text-xs text-white/35">Analysis confidence: {confidence}</p> : null}
        </div>
        <div className="rounded-xl border border-white/10 bg-black/20 p-4">
          <div className="flex items-center gap-2 text-sm font-medium text-white/80">
            <MessageSquareText size={16} className="text-cyanGlow" /> Reflection understanding
          </div>
          <p className="mt-2 text-sm leading-6 text-white/55">{reflectionAssessment}</p>
        </div>
      </div>
    </section>
  );
}
