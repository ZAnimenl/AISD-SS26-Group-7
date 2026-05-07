"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { getAdminAssessments, getStudentAssessments } from "@/lib/api";
import type { Assessment } from "@/lib/types";

type TopBarRole = "student" | "administrator";

interface SearchItem {
  label: string;
  href: string;
  keywords: string;
  group?: string;
}

const studentSearchItems: SearchItem[] = [
  { label: "Dashboard", href: "/student/dashboard", keywords: "student dashboard home overview", group: "Page" },
  { label: "Assessments", href: "/student/assessments", keywords: "assessments coding tests start continue workspace", group: "Page" },
  { label: "Results", href: "/student/results", keywords: "results scores submissions review", group: "Page" }
];

const adminSearchItems: SearchItem[] = [
  { label: "Dashboard", href: "/admin/dashboard", keywords: "administrator admin dashboard overview", group: "Page" },
  { label: "Assessments", href: "/admin/assessments", keywords: "assessments manage edit questions tests", group: "Page" },
  { label: "Create assessment", href: "/admin/assessments/new", keywords: "create new assessment author", group: "Page" },
  { label: "Reports", href: "/admin/reports", keywords: "reports analytics results submissions scores", group: "Page" },
  { label: "Users", href: "/admin/users", keywords: "users students administrators accounts roles", group: "Page" }
];

function toAssessmentSearchItem(assessment: Assessment, role: TopBarRole): SearchItem {
  const studentHref = assessment.attempt_status === "active"
    ? `/student/assessments/${assessment.assessment_id}/workspace`
    : assessment.attempt_status === "submitted"
    ? `/student/assessments/${assessment.assessment_id}/review`
    : `/student/assessments/${assessment.assessment_id}/start`;

  return {
    label: assessment.title,
    href: role === "student" ? studentHref : `/admin/assessments/${assessment.assessment_id}`,
    keywords: [
      "assessment",
      assessment.description,
      assessment.status,
      assessment.attempt_status,
      assessment.ai_enabled ? "ai enabled" : "ai disabled"
    ].filter(Boolean).join(" "),
    group: "Assessment"
  };
}

export function TopBar({ label, role }: { label: string; role: TopBarRole }) {
  const router = useRouter();
  const [query, setQuery] = useState("");
  const [focused, setFocused] = useState(false);
  const [assessmentItems, setAssessmentItems] = useState<SearchItem[]>([]);
  const staticSearchItems = role === "student" ? studentSearchItems : adminSearchItems;
  const searchItems = useMemo(() => [...staticSearchItems, ...assessmentItems], [assessmentItems, staticSearchItems]);
  const suggestions = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();

    if (!normalizedQuery) {
      return [];
    }

    return searchItems
      .filter((item) => `${item.label} ${item.keywords}`.toLowerCase().includes(normalizedQuery))
      .slice(0, 4);
  }, [query, searchItems]);

  useEffect(() => {
    let ignore = false;

    async function loadAssessments() {
      try {
        const assessments = role === "student" ? await getStudentAssessments() : await getAdminAssessments();

        if (!ignore) {
          setAssessmentItems(assessments.map((assessment) => toAssessmentSearchItem(assessment, role)));
        }
      } catch {
        if (!ignore) {
          setAssessmentItems([]);
        }
      }
    }

    loadAssessments();

    return () => {
      ignore = true;
    };
  }, [role]);

  function goToResult(item: SearchItem) {
    setQuery("");
    setFocused(false);
    router.push(item.href);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (suggestions[0]) {
      goToResult(suggestions[0]);
    }
  }

  return (
    <header className="liquid-glass dynamic-surface reveal-up relative z-50 m-4 mb-0 flex items-center gap-3 overflow-visible rounded-2xl px-4 py-3 lg:m-6 lg:mb-0">
      <form className="relative min-w-0 flex-1" onSubmit={handleSubmit}>
        <div className="flex items-center gap-3">
          <Search className="shrink-0 text-white/30" size={18} />
          <input
            aria-label="Search navigation"
            className="min-w-0 flex-1 bg-transparent text-sm text-white outline-none placeholder:text-white/45"
            placeholder={label}
            value={query}
            onBlur={() => window.setTimeout(() => setFocused(false), 120)}
            onChange={(event) => setQuery(event.target.value)}
            onFocus={() => setFocused(true)}
          />
        </div>
        {focused && query.trim() ? (
          <div className="absolute left-0 right-0 top-12 z-[60] overflow-hidden rounded-xl border border-cyanGlow/20 bg-[#050814] shadow-[0_18px_50px_rgba(0,0,0,0.72),0_0_28px_rgba(0,229,255,0.12)]">
            {suggestions.length ? (
              suggestions.map((item) => (
                <button
                  key={item.href}
                  className="flex w-full items-center justify-between px-4 py-3 text-left text-sm text-white/75 transition hover:bg-white/8 hover:text-cyanGlow"
                  type="button"
                  onClick={() => goToResult(item)}
                >
                  <span className="min-w-0">
                    <span className="block truncate">{item.label}</span>
                    {item.group ? <span className="block text-xs text-white/35">{item.group}</span> : null}
                  </span>
                </button>
              ))
            ) : (
              <p className="px-4 py-3 text-sm text-white/45">No matching page</p>
            )}
          </div>
        ) : null}
      </form>
      <div className="grid grid-cols-2 gap-1">
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-cyanGlow" />
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-purpleGlow [animation-delay:0.25s]" />
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-purpleGlow [animation-delay:0.5s]" />
        <span className="pulse-dot h-1.5 w-1.5 rounded-full bg-cyanGlow [animation-delay:0.75s]" />
      </div>
      <div className="flex items-center gap-2 rounded-full border border-white/10 bg-white/5 p-1 pl-2 transition hover:border-cyanGlow/30 hover:bg-white/10">
        <span className="text-xs text-white/55">Demo</span>
        <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-cyanGlow to-purpleGlow text-xs font-bold text-slate-950">AI</span>
      </div>
    </header>
  );
}
