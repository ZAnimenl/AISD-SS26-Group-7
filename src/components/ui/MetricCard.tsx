import type { LucideIcon } from "lucide-react";
import { ScoreDonut } from "@/components/reports/ScoreDonut";

interface MetricCardProps {
  label: string;
  value: string | number;
  icon: LucideIcon;
  score?: number;
}

export function MetricCard({ label, value, icon: Icon, score }: MetricCardProps) {
  return (
    <article className="metric-card dynamic-surface text-center">
      <div className="relative flex flex-col items-center">
        {score == null ? (
          <span className="float-soft grid h-12 w-12 place-items-center rounded-2xl border border-cyanGlow/20 bg-cyanGlow/10 text-cyanGlow">
            <Icon size={20} />
          </span>
        ) : null}
        <p className={`${score == null ? "mt-4" : ""} text-xs font-medium uppercase tracking-[0.16em] text-white/40`}>{label}</p>
        {score != null ? (
          <div className="mt-3"><ScoreDonut value={score} size={58} label={label} /></div>
        ) : (
          <p className="mt-2 text-3xl font-semibold text-white">{value}</p>
        )}
      </div>
      <div className="relative mt-5 h-1 overflow-hidden rounded-full bg-white/8">
        <span className="absolute inset-y-0 left-1/2 w-2/3 -translate-x-1/2 rounded-full bg-gradient-to-r from-cyanGlow via-purpleGlow to-pinkGlow opacity-70" />
      </div>
    </article>
  );
}
