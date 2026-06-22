"use client";

import { Minus, Plus } from "lucide-react";

const MIN_DURATION_MINUTES = 1;
const MAX_DURATION_MINUTES = 240;
const DURATION_BUTTON_STEP = 5;

export function DurationSlider({
  value,
  onChange,
  disabled = false
}: {
  value: number;
  onChange: (value: number) => void;
  disabled?: boolean;
}) {
  const update = (nextValue: number) => {
    onChange(Math.max(MIN_DURATION_MINUTES, Math.min(MAX_DURATION_MINUTES, Math.round(nextValue))));
  };

  return (
    <div className="grid gap-3 rounded-xl border border-white/10 bg-black/20 p-4">
      <input type="hidden" name="duration_minutes" value={value} />
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm text-white/60">Duration</span>
        <label className="flex items-center rounded-lg border border-cyanGlow/25 bg-cyanGlow/10 px-2 py-1 text-cyanGlow focus-within:border-cyanGlow/70">
          <span className="sr-only">Duration in minutes</span>
          <input
            className="w-14 appearance-none bg-transparent text-right font-mono text-sm text-cyanGlow outline-none disabled:opacity-45"
            type="number"
            min={MIN_DURATION_MINUTES}
            max={MAX_DURATION_MINUTES}
            step={1}
            value={value}
            disabled={disabled}
            onChange={(event) => {
              if (event.target.value !== "") {
                update(Number(event.target.value));
              }
            }}
          />
          <span className="ml-1 font-mono text-sm">min</span>
        </label>
      </div>
      <div className="grid grid-cols-[36px_1fr_36px] items-center gap-3">
        <button
          type="button"
          className="grid h-9 w-9 place-items-center rounded-full border border-white/15 bg-black/20 text-white/65 transition hover:border-cyanGlow/45 hover:text-cyanGlow disabled:opacity-30"
          aria-label="Decrease duration by five minutes"
          disabled={disabled || value <= MIN_DURATION_MINUTES}
          onClick={() => update(value - DURATION_BUTTON_STEP)}
        >
          <Minus size={15} />
        </button>
        <input
          className="h-2 w-full cursor-pointer accent-cyan-400 disabled:cursor-not-allowed disabled:opacity-45"
          type="range"
          min={MIN_DURATION_MINUTES}
          max={MAX_DURATION_MINUTES}
          step={1}
          value={value}
          aria-label="Duration in minutes"
          disabled={disabled}
          onChange={(event) => update(Number(event.target.value))}
        />
        <button
          type="button"
          className="grid h-9 w-9 place-items-center rounded-full border border-white/15 bg-black/20 text-white/65 transition hover:border-cyanGlow/45 hover:text-cyanGlow disabled:opacity-30"
          aria-label="Increase duration by five minutes"
          disabled={disabled || value >= MAX_DURATION_MINUTES}
          onClick={() => update(value + DURATION_BUTTON_STEP)}
        >
          <Plus size={15} />
        </button>
      </div>
      <div className="flex justify-between text-[11px] text-white/30" aria-hidden="true">
        <span>{MIN_DURATION_MINUTES} min</span>
        <span>{MAX_DURATION_MINUTES} min</span>
      </div>
    </div>
  );
}
