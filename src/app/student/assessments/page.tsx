"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AssessmentCard } from "@/components/student/AssessmentCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getStudentAssessments, isAuthenticationError } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function StudentAssessmentsPage() {
  const router = useRouter();
  const [assessments, setAssessments] = useState<Assessment[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getStudentAssessments()
      .then((nextAssessments) => {
        setAssessments(nextAssessments);
        setError(null);
      })
      .catch((exception) => {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load assessments.");
      });
  }, [router]);

  return (
    <div>
      <SectionHeader eyebrow="Student" title="Assessments" />
      {error ? <section className="panel text-sm text-pinkGlow">{error}</section> : null}
      {assessments === null ? (
        <section className="panel text-sm text-white/55" aria-live="polite">Loading available assessments from backend...</section>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {assessments.map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
        </div>
      )}
    </div>
  );
}
