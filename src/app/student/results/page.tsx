"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3 } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getStudentResults, isAuthenticationError } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function StudentResultsPage() {
  const router = useRouter();
  const [results, setResults] = useState<Assessment[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getStudentResults()
      .then((nextResults) => {
        setResults(nextResults);
        setError(null);
      })
      .catch((exception) => {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load results.");
      });
  }, [router]);

  return (
    <div>
      <SectionHeader eyebrow="Student" title="Results" />
      {error ? <section className="panel text-sm text-pinkGlow">{error}</section> : null}
      <section className="panel">
        <div className="relative overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="text-xs uppercase tracking-[0.14em] text-white/35">
              <tr>
                <th className="pb-3">Assessment</th>
                <th className="pb-3">Status</th>
                <th className="pb-3">Score</th>
                <th className="pb-3">AI summary</th>
                <th className="pb-3">Report</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/10">
              {results.map((result) => (
                <tr key={result.assessment_id}>
                  <td className="py-4 text-white">{result.title}</td>
                  <td className="py-4"><StatusBadge status="submitted" /></td>
                  <td className="py-4 text-cyanGlow">{result.score}%</td>
                  <td className="py-4 text-white/55">{result.ai_enabled ? "AI enabled" : "AI disabled"}</td>
                  <td className="py-4">
                    <Link
                      href={`/student/assessments/${result.assessment_id}/review?submissionId=${result.submission_id}`}
                      className="btn-secondary px-2.5 py-1.5 text-xs"
                    >
                      <BarChart3 size={14} />
                      Report
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
