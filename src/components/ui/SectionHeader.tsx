import type { ReactNode } from "react";

export function SectionHeader({ title, eyebrow, action }: { title: string; eyebrow?: string; action?: ReactNode }) {
  return (
    <div className="reveal-up mb-4 flex flex-wrap items-end justify-between gap-3">
      <div>
        {eyebrow ? <p className="text-xs font-medium uppercase tracking-[0.18em] text-cyanGlow/70">{eyebrow}</p> : null}
        <h1 className="live-gradient-text font-heading text-4xl italic">{title}</h1>
      </div>
      {action}
    </div>
  );
}
