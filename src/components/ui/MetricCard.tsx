import type { LucideIcon } from "lucide-react";

interface MetricCardProps {
  label: string;
  value: string | number;
  detail: string;
  icon: LucideIcon;
}

export function MetricCard({ label, value, detail, icon: Icon }: MetricCardProps) {
  return (
    <article className="metric-card dynamic-surface">
      <div className="relative flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-medium uppercase tracking-[0.16em] text-white/40">{label}</p>
          <p className="mt-3 text-3xl font-semibold text-white">{value}</p>
          <p className="mt-1 text-sm text-white/50">{detail}</p>
        </div>
        <span className="float-soft rounded-2xl border border-cyanGlow/20 bg-cyanGlow/10 p-3 text-cyanGlow">
          <Icon size={20} />
        </span>
      </div>
      <div className="relative mt-5 h-1 overflow-hidden rounded-full bg-white/8">
        <span className="absolute inset-y-0 left-0 w-2/3 rounded-full bg-gradient-to-r from-cyanGlow via-purpleGlow to-pinkGlow opacity-70" />
      </div>
    </article>
  );
}
