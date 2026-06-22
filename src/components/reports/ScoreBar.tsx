interface ScoreBarProps {
  value: number;
  tone?: "cyan" | "purple";
  label?: string;
}

export function ScoreBar({ value, tone = "cyan", label = "Score" }: ScoreBarProps) {
  const score = Math.max(0, Math.min(100, Math.round(value)));
  const gradient = tone === "purple"
    ? "from-purpleGlow to-pinkGlow"
    : "from-cyanGlow to-purpleGlow";

  return (
    <div className="w-full" role="img" aria-label={`${label}: ${score}%`}>
      <p className="text-3xl font-semibold text-white">{score}%</p>
      <div className="mt-4 h-2 overflow-hidden rounded-full bg-white/10">
        <div
          className={`h-full rounded-full bg-gradient-to-r ${gradient} shadow-[0_0_14px_rgba(0,229,255,0.28)] transition-[width] duration-700`}
          style={{ width: `${score}%` }}
        />
      </div>
    </div>
  );
}
