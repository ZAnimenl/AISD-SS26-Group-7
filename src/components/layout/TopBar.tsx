import { ChevronDown, Search } from "lucide-react";

export function TopBar({ label }: { label: string }) {
  return (
    <header className="liquid-glass dynamic-surface reveal-up m-4 mb-0 flex items-center gap-3 rounded-2xl px-4 py-3 lg:m-6 lg:mb-0">
      <Search className="text-white/30" size={18} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm text-white/45">{label}</p>
      </div>
      <div className="grid grid-cols-2 gap-1">
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-cyanGlow" />
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-purpleGlow [animation-delay:0.25s]" />
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-purpleGlow [animation-delay:0.5s]" />
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-cyanGlow [animation-delay:0.75s]" />
      </div>
      <div className="flex items-center gap-2 rounded-full border border-white/10 bg-white/5 p-1 pl-2 transition hover:border-cyanGlow/30 hover:bg-white/10">
        <span className="text-xs text-white/55">Demo</span>
        <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-cyanGlow to-purpleGlow text-xs font-bold text-slate-950">AI</span>
        <ChevronDown size={16} className="mr-1 text-white/40" />
      </div>
    </header>
  );
}
