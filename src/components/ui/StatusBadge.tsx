const toneMap: Record<string, string> = {
  active: "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow",
  not_started: "border-white/10 bg-white/5 text-white/70",
  not_submitted: "border-white/10 bg-white/5 text-white/60",
  submitted: "border-purpleGlow/30 bg-purpleGlow/10 text-purpleGlow",
  closed: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow",
  expired: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow",
  scheduled: "border-amber-500/30 bg-amber-500/10 text-amber-300",
  draft: "border-white/10 bg-white/5 text-white/60",
  archived: "border-white/10 bg-black/20 text-white/40",
  passed: "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow",
  failed: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow",
  runtime_error: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow",
  time_limit_exceeded: "border-amber-500/30 bg-amber-500/10 text-amber-300",
  memory_limit_exceeded: "border-amber-500/30 bg-amber-500/10 text-amber-300",
  internal_error: "border-pinkGlow/30 bg-pinkGlow/10 text-pinkGlow",
  queued: "border-white/10 bg-white/5 text-white/60",
  running: "border-cyanGlow/30 bg-cyanGlow/10 text-cyanGlow"
};

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge ${toneMap[status] ?? ""}`}>{status.replaceAll("_", " ")}</span>;
}
