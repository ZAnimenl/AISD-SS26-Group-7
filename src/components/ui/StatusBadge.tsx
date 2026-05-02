const toneMap: Record<string, string> = {
  active: "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow",
  not_started: "border-white/10 bg-white/5 text-white/70",
  submitted: "border-purpleGlow/30 bg-purpleGlow/10 text-purpleGlow",
  closed: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow",
  draft: "border-white/10 bg-white/5 text-white/60",
  archived: "border-white/10 bg-black/20 text-white/40",
  passed: "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow",
  failed: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow"
};

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge ${toneMap[status] ?? ""}`}>{status.replaceAll("_", " ")}</span>;
}
