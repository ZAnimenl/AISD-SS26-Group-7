"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, BarChart3, CheckCircle2, FileCode2, Home } from "lucide-react";
import { getStudentResults } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import type { Assessment } from "@/lib/types";

export default function StudentAssessmentReviewPage({ params }: { params: { assessmentId: string } }) {
  const router = useRouter();
  const [results, setResults] = useState<Assessment[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        setResults(await getStudentResults());
      } catch {
        router.replace("/login");
      } finally {
        setIsLoading(false);
      }
    }

    load();
  }, [router]);

  const result = useMemo(
    () => results.find((item) => item.assessment_id === params.assessmentId),
    [params.assessmentId, results]
  );

  if (isLoading) {
    return <SectionHeader eyebrow="Assessment review" title="Loading result..." />;
  }

  if (!result) {
    return (
      <div>
        <SectionHeader
          eyebrow="Assessment review"
          title="Result not available yet"
          action={<Link className="btn-secondary" href="/student/results"><ArrowLeft size={16} /> All results</Link>}
        />
        <section className="panel">
          <p className="relative text-white/60">
            The submission was received, but the published result for this assessment is not available in the student results feed yet.
          </p>
          <div className="relative mt-6 flex flex-wrap gap-3">
            <Link className="btn-primary" href="/student/dashboard"><Home size={16} /> Dashboard</Link>
            <Link className="btn-secondary" href="/student/results"><BarChart3 size={16} /> Results</Link>
          </div>
        </section>
      </div>
    );
  }

  return (
    <div>
      <SectionHeader
        eyebrow="Assessment review"
        title={result.title}
        action={<Link className="btn-primary" href="/student/dashboard"><Home size={16} /> Dashboard</Link>}
      />
      <div className="grid gap-4 md:grid-cols-3">
        <section className="metric-card">
          <div className="relative flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow"><CheckCircle2 size={20} /></span>
            <div>
              <p className="text-sm text-white/45">Status</p>
              <StatusBadge status="submitted" />
            </div>
          </div>
        </section>
        <section className="metric-card">
          <div className="relative flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow"><BarChart3 size={20} /></span>
            <div>
              <p className="text-sm text-white/45">Score</p>
              <p className="text-2xl font-semibold text-cyanGlow">{result.score ?? 0}%</p>
            </div>
          </div>
        </section>
        <section className="metric-card">
          <div className="relative flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow"><FileCode2 size={20} /></span>
            <div>
              <p className="text-sm text-white/45">Questions</p>
              <p className="text-2xl font-semibold text-white">{result.question_count}</p>
            </div>
          </div>
        </section>
      </div>
      <section className="panel mt-6">
        <div className="relative">
          <h2 className="text-lg font-semibold">Submission summary</h2>
          <p className="mt-3 max-w-3xl leading-7 text-white/60">
            Your final submission for this assessment has been recorded. Hidden test inputs and expected outputs remain private; only the published score and status are shown here.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link className="btn-primary" href="/student/dashboard"><Home size={16} /> Back to dashboard</Link>
            <Link className="btn-secondary" href="/student/results"><BarChart3 size={16} /> View all results</Link>
          </div>
        </div>
      </section>
    </div>
  );
}
