"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BarChart3 } from "lucide-react";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getStudentResults } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function StudentResultsPage() {
  const router = useRouter();
  const [results, setResults] = useState<Assessment[]>([]);

  useEffect(() => {
    getStudentResults().then(setResults).catch(() => router.replace("/login"));
  }, [router]);

  return (
    <div>
      <SectionHeader eyebrow="Student" title="Results" />
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
                  <td className="py-4 text-white/55">{result.ai_enabled ? "3 interactions used" : "AI disabled"}</td>
                  <td className="py-4 text-white/45"><BarChart3 size={18} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
