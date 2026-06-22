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
    <article className="metric-card dynamic-surface flex h-full flex-col text-center">
      <div className="relative flex flex-1 flex-col items-center">
        <div className="grid h-14 place-items-center">
          {score == null ? (
          <span className="float-soft grid h-12 w-12 place-items-center rounded-2xl border border-cyanGlow/20 bg-cyanGlow/10 text-cyanGlow">
            <Icon size={20} />
          </span>
          ) : (
            <ScoreDonut value={score} size={54} label={label} />
          )}
        </div>
        <p className="mt-4 text-xs font-medium uppercase tracking-[0.16em] text-white/40">{label}</p>
        {score != null ? (
          <span className="sr-only">{value}</span>
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
