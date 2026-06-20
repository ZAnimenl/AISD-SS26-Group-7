"use client";

import { ChevronDown, Check } from "lucide-react";
import { type CSSProperties, useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";

export type DropdownOption<T extends string = string> = {
  value: T;
  label: string;
};

export function CustomDropdown<T extends string>({
  value,
  options,
  onChange,
  name,
  ariaLabel,
  disabled = false,
  className = ""
}: {
  value: T;
  options: DropdownOption<T>[];
  onChange: (value: T) => void;
  name?: string;
  ariaLabel: string;
  disabled?: boolean;
  className?: string;
}) {
  const listboxId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const [open, setOpen] = useState(false);
  const [menuStyle, setMenuStyle] = useState<CSSProperties>({});
  const [activeIndex, setActiveIndex] = useState(() => Math.max(0, options.findIndex((option) => option.value === value)));
  const selectedIndex = Math.max(0, options.findIndex((option) => option.value === value));
  const selected = options[selectedIndex];

  useEffect(() => {
    if (!open) return;
    const close = (event: MouseEvent) => {
      const target = event.target as Node;
      if (!rootRef.current?.contains(target) && !menuRef.current?.contains(target)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);

  useLayoutEffect(() => {
    if (!open) return;

    const positionMenu = () => {
      const trigger = rootRef.current?.getBoundingClientRect();
      if (!trigger) return;

      const viewportPadding = 8;
      const menuGap = 8;
      const preferredHeight = Math.min(256, Math.max(48, options.length * 43 + 12));
      const spaceBelow = window.innerHeight - trigger.bottom - menuGap - viewportPadding;
      const spaceAbove = trigger.top - menuGap - viewportPadding;
      const openAbove = spaceBelow < Math.min(preferredHeight, 160) && spaceAbove > spaceBelow;
      const availableHeight = Math.max(80, openAbove ? spaceAbove : spaceBelow);

      setMenuStyle({
        left: Math.max(viewportPadding, Math.min(trigger.left, window.innerWidth - trigger.width - viewportPadding)),
        width: Math.min(trigger.width, window.innerWidth - viewportPadding * 2),
        maxHeight: Math.min(preferredHeight, availableHeight),
        ...(openAbove
          ? { bottom: window.innerHeight - trigger.top + menuGap }
          : { top: trigger.bottom + menuGap })
      });
    };

    positionMenu();
    window.addEventListener("resize", positionMenu);
    window.addEventListener("scroll", positionMenu, true);
    return () => {
      window.removeEventListener("resize", positionMenu);
      window.removeEventListener("scroll", positionMenu, true);
    };
  }, [open, options.length]);

  useEffect(() => {
    if (open) optionRefs.current[activeIndex]?.focus();
  }, [activeIndex, open]);

  function move(direction: 1 | -1) {
    if (options.length === 0) return;
    setActiveIndex((current) => (current + direction + options.length) % options.length);
  }

  function choose(option: DropdownOption<T>) {
    onChange(option.value);
    setOpen(false);
  }

  return (
    <div ref={rootRef} className={`relative ${className}`}>
      {name ? <input type="hidden" name={name} value={value} /> : null}
      <button
        type="button"
        className="field flex w-full items-center justify-between gap-3 text-left capitalize disabled:cursor-not-allowed disabled:opacity-45"
        aria-label={ariaLabel}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listboxId}
        disabled={disabled}
        onClick={() => {
          setActiveIndex(selectedIndex);
          setOpen((current) => !current);
        }}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            if (!open) {
              const direction = event.key === "ArrowDown" ? 1 : -1;
              setActiveIndex((selectedIndex + direction + options.length) % options.length);
              setOpen(true);
            } else {
              move(event.key === "ArrowDown" ? 1 : -1);
            }
          } else if (event.key === "Escape") {
            setOpen(false);
          }
        }}
      >
        <span>{selected?.label ?? value}</span>
        <ChevronDown size={17} className={`shrink-0 text-white/45 transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && typeof document !== "undefined" ? createPortal(
        <div
          ref={menuRef}
          id={listboxId}
          role="listbox"
          aria-label={`${ariaLabel} options`}
          style={menuStyle}
          className="fixed z-[100] overflow-y-auto rounded-xl border border-cyanGlow/25 bg-[#0b1422] p-1.5 shadow-[0_18px_48px_rgba(0,0,0,0.55)]"
        >
          {options.map((option, index) => {
            const isSelected = option.value === value;
            return (
              <button
                key={option.value}
                ref={(element) => { optionRefs.current[index] = element; }}
                type="button"
                role="option"
                aria-selected={isSelected}
                tabIndex={index === activeIndex ? 0 : -1}
                className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm capitalize transition ${
                  isSelected ? "bg-cyanGlow/12 text-cyanGlow" : "text-white/70 hover:bg-white/8 hover:text-white"
                }`}
                onClick={() => choose(option)}
                onKeyDown={(event) => {
                  if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                    event.preventDefault();
                    move(event.key === "ArrowDown" ? 1 : -1);
                  } else if (event.key === "Home") {
                    event.preventDefault();
                    setActiveIndex(0);
                  } else if (event.key === "End") {
                    event.preventDefault();
                    setActiveIndex(options.length - 1);
                  } else if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    choose(option);
                  } else if (event.key === "Escape" || event.key === "Tab") {
                    setOpen(false);
                  }
                }}
              >
                <span>{option.label}</span>
                {isSelected ? <Check size={15} /> : null}
              </button>
            );
          })}
        </div>,
        document.body
      ) : null}
    </div>
  );
}
