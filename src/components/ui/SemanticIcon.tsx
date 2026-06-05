"use client";

import type { ReactNode, SVGProps } from "react";

export type SemanticIconName =
  | "ai"
  | "api"
  | "assessments"
  | "bug"
  | "check"
  | "clock"
  | "close"
  | "collapse"
  | "console"
  | "create"
  | "dashboard"
  | "database"
  | "debugging"
  | "expand"
  | "explanation"
  | "fail"
  | "file"
  | "folder"
  | "frontend"
  | "logout"
  | "platform"
  | "play"
  | "preview"
  | "reports"
  | "results"
  | "send"
  | "settings"
  | "suggestion"
  | "users";

interface SemanticIconProps extends Omit<SVGProps<SVGSVGElement>, "name"> {
  name: SemanticIconName;
  size?: number;
}

const iconPaths: Record<SemanticIconName, ReactNode> = {
  platform: (
    <>
      <path d="M7 8.5h6.5L17 12l-3.5 3.5H7L3.5 12 7 8.5Z" />
      <path d="M8.5 5 12 3l3.5 2M8.5 19 12 21l3.5-2" opacity="0.65" />
      <path d="M9.3 12h5.4M12 9.3v5.4" opacity="0.9" />
    </>
  ),
  dashboard: (
    <>
      <path d="M4 5h7v6H4V5ZM14 5h6v4h-6V5ZM14 12h6v7h-6v-7ZM4 14h7v5H4v-5Z" />
      <path d="M7 8h1M17 7h1M17 15h1M7 16h1" opacity="0.6" />
    </>
  ),
  assessments: (
    <>
      <path d="M5 5h14v14H5V5Z" />
      <path d="m10 9-2.5 3 2.5 3M14 9l2.5 3-2.5 3M12.8 8.5l-1.6 7" opacity="0.85" />
    </>
  ),
  results: (
    <>
      <path d="M5 19h14" />
      <path d="M7 16v-4M12 16V8M17 16v-7" />
      <path d="m6.5 7.5 3 2.5 3-4 4.5 2.5" opacity="0.72" />
    </>
  ),
  create: (
    <>
      <path d="M6 4h8l4 4v12H6V4Z" />
      <path d="M14 4v4h4M12 10v6M9 13h6" opacity="0.85" />
    </>
  ),
  reports: (
    <>
      <path d="M6 4h12v16H6V4Z" />
      <path d="M9 15h6M9 11h6M9 7h3M10 18l2-2 2 2" opacity="0.75" />
    </>
  ),
  users: (
    <>
      <path d="M9.5 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM15.5 10.5a2.5 2.5 0 1 0 0-5" />
      <path d="M4.5 19c.7-3 2.7-4.5 6-4.5s5.3 1.5 6 4.5M16 14.5c2.3.3 3.8 1.8 4.5 4.5" opacity="0.75" />
    </>
  ),
  logout: (
    <>
      <path d="M10 5H6v14h4" />
      <path d="M12 12h9M17 8l4 4-4 4" />
    </>
  ),
  settings: (
    <>
      <path d="M12 8.2a3.8 3.8 0 1 0 0 7.6 3.8 3.8 0 0 0 0-7.6Z" />
      <path d="m4.8 10 .8-2 2.2.2 1.2-1.1.3-2.2h3.4l.3 2.2 1.2 1.1 2.2-.2.8 2-1.5 1.6v1.8l1.5 1.6-.8 2-2.2-.2-1.2 1.1-.3 2.2H9.3l-.3-2.2-1.2-1.1-2.2.2-.8-2 1.5-1.6v-1.8L4.8 10Z" opacity="0.6" />
    </>
  ),
  frontend: (
    <>
      <path d="M4 6h16v12H4V6Z" />
      <path d="M8 10h4M8 14h8M16 10h.01" opacity="0.75" />
    </>
  ),
  api: (
    <>
      <path d="M7 8h10M7 16h10M6 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4ZM18 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z" />
      <path d="M18 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4ZM6 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z" opacity="0.72" />
    </>
  ),
  database: (
    <>
      <path d="M5 7c0-2 3.1-3 7-3s7 1 7 3-3.1 3-7 3-7-1-7-3Z" />
      <path d="M5 7v5c0 2 3.1 3 7 3s7-1 7-3V7M5 12v5c0 2 3.1 3 7 3s7-1 7-3v-5" opacity="0.8" />
    </>
  ),
  bug: (
    <>
      <path d="M8 9.5a4 4 0 0 1 8 0V16a4 4 0 0 1-8 0V9.5Z" />
      <path d="M9 5.5 7 3M15 5.5 17 3M4 11h4M16 11h4M4.5 17H8M16 17h3.5M12 10v8" opacity="0.75" />
    </>
  ),
  ai: (
    <>
      <path d="M9 4.5a3 3 0 0 1 3 3v9a3 3 0 0 1-3 3M15 4.5a3 3 0 0 0-3 3v9a3 3 0 0 0 3 3" />
      <path d="M7.5 8.5H5.8a2.3 2.3 0 0 0 0 4.6H7M16.5 8.5h1.7a2.3 2.3 0 0 1 0 4.6H17M8 15h8M8 11h8" opacity="0.78" />
    </>
  ),
  suggestion: (
    <>
      <path d="M12 4v3M12 17v3M4 12h3M17 12h3" />
      <path d="m7 7 2.2 2.2M17 7l-2.2 2.2M7 17l2.2-2.2M17 17l-2.2-2.2" opacity="0.68" />
      <path d="M12 9.5 13.4 12 12 14.5 10.6 12 12 9.5Z" />
    </>
  ),
  explanation: (
    <>
      <path d="M5 6h14v12H5V6Z" />
      <path d="M8 10h8M8 13h5M16 16l3 3" opacity="0.75" />
    </>
  ),
  debugging: (
    <>
      <path d="M8 8h8v8H8V8Z" />
      <path d="M12 4v4M12 16v4M4 12h4M16 12h4M6 6l2.5 2.5M18 6l-2.5 2.5M6 18l2.5-2.5M18 18l-2.5-2.5" opacity="0.72" />
    </>
  ),
  file: (
    <>
      <path d="M7 4h7l4 4v12H7V4Z" />
      <path d="M14 4v4h4M10 12h5M10 16h5" opacity="0.78" />
    </>
  ),
  folder: (
    <>
      <path d="M4 7h6l2 2h8v10H4V7Z" />
      <path d="M4 11h16" opacity="0.72" />
    </>
  ),
  clock: (
    <>
      <path d="M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z" />
      <path d="M12 7v5l3 2" opacity="0.82" />
    </>
  ),
  play: (
    <>
      <path d="M8 5v14l11-7L8 5Z" />
      <path d="M4 5v14" opacity="0.55" />
    </>
  ),
  preview: (
    <>
      <path d="M3.5 12s3.2-5 8.5-5 8.5 5 8.5 5-3.2 5-8.5 5-8.5-5-8.5-5Z" />
      <path d="M12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5Z" opacity="0.8" />
    </>
  ),
  console: (
    <>
      <path d="M4 6h16v12H4V6Z" />
      <path d="m8 10 2 2-2 2M12 15h5" opacity="0.85" />
    </>
  ),
  send: (
    <>
      <path d="m4 12 17-8-8 17-2-7-7-2Z" />
      <path d="m11 14 4-5" opacity="0.75" />
    </>
  ),
  check: (
    <>
      <path d="M20 6 9 17l-5-5" />
    </>
  ),
  fail: (
    <>
      <path d="M7 7l10 10M17 7 7 17" />
    </>
  ),
  close: (
    <>
      <path d="M7 7l10 10M17 7 7 17" />
    </>
  ),
  collapse: (
    <>
      <path d="M5 15h14M8 9l4 4 4-4" />
    </>
  ),
  expand: (
    <>
      <path d="M5 9h14M8 15l4-4 4 4" />
    </>
  )
};

export function SemanticIcon({ name, size = 18, className, ...props }: SemanticIconProps) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      fill="none"
      height={size}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      viewBox="0 0 24 24"
      width={size}
      {...props}
    >
      {iconPaths[name]}
    </svg>
  );
}
