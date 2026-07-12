interface AiRubricBreakdownProps {
  details?: Record<string, unknown>;
}

interface RubricCriterion {
  key: string;
  label: string;
  maximum: number;
  aliases: string[];
  keywords: string[];
  score: (details: Record<string, unknown>) => number | null;
}

function normalizeKey(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase().replace(/[^a-z0-9]+/g, "_");
}

function numericCriterion(details: Record<string, unknown>, key: string) {
  const criteria = details.criteria;
  if (!criteria || typeof criteria !== "object") return null;

  const entries = Object.entries(criteria as Record<string, unknown>);
  const value = entries.find(([candidate]) => normalizeKey(candidate) === key)?.[1];
  return typeof value === "number" ? value : null;
}

function evidenceText(item: unknown) {
  if (typeof item === "string") return item.trim();
  if (!item || typeof item !== "object" || Array.isArray(item)) return "";

  const record = item as Record<string, unknown>;
  for (const key of ["summary", "reason", "evidence", "description", "detail", "observation", "text"]) {
    if (typeof record[key] === "string" && record[key].trim()) {
      return record[key].trim();
    }
  }
  return "";
}

function criterionEvidence(details: Record<string, unknown>, aliases: string[], keywords: string[]) {
  const evidence = Array.isArray(details.evidence)
    ? details.evidence
    : details.evidence == null
      ? []
      : [details.evidence];
  const keyedMatches = evidence.flatMap((item) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) return [];
    return Object.entries(item as Record<string, unknown>).flatMap(([key, value]) =>
      aliases.some((alias) => normalizeKey(key).includes(alias))
        && typeof value === "string"
        && value.trim()
        ? [value.trim()]
        : []
    );
  });
  const matches = evidence.flatMap((item) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) return [];
    const record = item as Record<string, unknown>;
    const criterion = ["criterion", "category", "rubric_criterion", "name"]
      .map((key) => record[key])
      .find((value) => typeof value === "string");
    if (typeof criterion !== "string") return [];

    const normalizedCriterion = normalizeKey(criterion);
    if (!aliases.some((alias) => normalizedCriterion.includes(alias))) return [];

    const text = evidenceText(item);
    return text ? [text] : [];
  });

  const explicitSummary = Array.from(new Set([...keyedMatches, ...matches])).slice(0, 2).join(" ");
  if (explicitSummary) return explicitSummary;

  const sharedSegments = evidence
    .map(evidenceText)
    .flatMap((text) => text.split(/(?<=[.!?;])\s+/))
    .map(readableSegment)
    .filter((text) => text && keywords.some((keyword) => normalizeKey(text).includes(keyword)));
  return Array.from(new Set(sharedSegments)).slice(0, 1).join(" ");
}

function concise(value: string) {
  const maximumLength = 150;
  if (value.length <= maximumLength) return value;

  const shortened = value.slice(0, maximumLength - 3);
  const lastSpace = shortened.lastIndexOf(" ");
  return `${shortened.slice(0, lastSpace > 0 ? lastSpace : undefined).trimEnd()}...`;
}

function readableSegment(value: string) {
  const cleaned = value.replaceAll("_", " ").trim().replace(/;\s*$/, ".");
  return cleaned ? cleaned.charAt(0).toUpperCase() + cleaned.slice(1) : "";
}

const criteria: RubricCriterion[] = [
  {
    key: "prompt_quality_and_context",
    label: "Prompt quality and context",
    maximum: 30,
    aliases: ["prompt_quality_and_context", "prompt_quality", "prompt"],
    keywords: ["prompt", "context", "specific", "requirement"],
    score: (details) => numericCriterion(details, "prompt_quality_and_context")
  },
  {
    key: "token_and_interaction_efficiency",
    label: "Token and interaction efficiency",
    maximum: 40,
    aliases: ["token_and_interaction_efficiency", "behavioral_efficiency", "objective_repetition", "efficiency"],
    keywords: ["objective_repetition", "repet", "token", "progress"],
    score: (details) => {
      const behavioral = numericCriterion(details, "behavioral_efficiency");
      const repetition = numericCriterion(details, "objective_repetition");
      return behavioral == null && repetition == null ? null : (behavioral ?? 0) + (repetition ?? 0);
    }
  },
  {
    key: "critical_evaluation_and_adaptation",
    label: "Critical evaluation and adaptation",
    maximum: 20,
    aliases: ["critical_evaluation_and_adaptation", "critical_evaluation_before_deduction", "critical_evaluation", "adaptation"],
    keywords: ["execution", "code_change", "apply", "verif", "test", "critical", "adapt"],
    score: (details) => numericCriterion(details, "critical_evaluation_and_adaptation")
  },
  {
    key: "reflection_quality_and_consistency",
    label: "Reflection quality and consistency",
    maximum: 10,
    aliases: ["reflection_quality_and_consistency", "reflection_quality", "reflection_consistency", "reflection"],
    keywords: ["reflection", "consisten", "contradict", "understand", "log"],
    score: (details) => numericCriterion(details, "reflection_quality_and_consistency")
  }
];

export function AiRubricBreakdown({ details = {} }: AiRubricBreakdownProps) {
  return (
    <section className="relative mt-5 rounded-2xl border border-white/10 bg-black/20 p-5">
      <p className="text-xs uppercase tracking-[0.14em] text-purpleGlow/80">AI scoring</p>
      <h3 className="mt-1 text-lg font-semibold">Rubric breakdown</h3>
      <div className="mt-4 grid gap-3">
        {criteria.map((criterion) => {
          const score = criterion.score(details);
          const summary = criterionEvidence(details, criterion.aliases, criterion.keywords);
          return (
            <article key={criterion.key} className="rounded-xl border border-white/10 bg-white/[0.035] p-4">
              <div className="flex items-start justify-between gap-4">
                <h4 className="font-medium text-white/85">{criterion.label}</h4>
                <span className="shrink-0 font-mono text-lg font-semibold text-cyanGlow">
                  {score ?? "—"}<span className="text-sm text-white/35">/{criterion.maximum}</span>
                </span>
              </div>
              <p className="mt-2 max-w-4xl text-sm leading-6 text-white/55">
                {summary
                  ? concise(summary)
                  : score == null
                    ? "This criterion has not been scored yet."
                    : "No criterion-specific narrative was returned; this score is based on the recorded assessment evidence."}
              </p>
            </article>
          );
        })}
      </div>
    </section>
  );
}
