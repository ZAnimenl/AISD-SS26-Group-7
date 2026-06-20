interface ScoreDonutProps {
  value: number;
  tone?: "cyan" | "purple";
  size?: number;
  label?: string;
}

export function ScoreDonut({ value, tone = "cyan", size = 52, label = "Score" }: ScoreDonutProps) {
  const score = Math.max(0, Math.min(100, Math.round(value)));
  const radius = 18;
  const circumference = 2 * Math.PI * radius;
  const dashOffset = circumference * (1 - score / 100);
  const stroke = tone === "purple" ? "#a855f7" : "#00e5ff";

  return (
    <div
      className="relative mx-auto grid shrink-0 place-items-center"
      style={{ width: size, height: size }}
      role="img"
      aria-label={`${label}: ${score}%`}
    >
      <svg className="-rotate-90" width={size} height={size} viewBox="0 0 48 48" aria-hidden="true">
        <circle cx="24" cy="24" r={radius} fill="none" stroke="rgba(255,255,255,0.09)" strokeWidth="5" />
        <circle
          cx="24"
          cy="24"
          r={radius}
          fill="none"
          stroke={stroke}
          strokeWidth="5"
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={dashOffset}
          className="drop-shadow-[0_0_6px_rgba(0,229,255,0.35)] transition-[stroke-dashoffset] duration-700"
        />
      </svg>
      <span className={`absolute font-semibold text-white/80 ${size >= 88 ? "text-base" : size >= 60 ? "text-xs" : "text-[9px]"}`}>{score}%</span>
    </div>
  );
}
