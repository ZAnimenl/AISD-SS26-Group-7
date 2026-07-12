"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AssessmentCard } from "@/components/student/AssessmentCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getStudentAssessments, isAuthenticationError } from "@/lib/api";
import { partitionAssessments } from "@/lib/assessmentSchedule";
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
        <section className="panel text-sm text-white/55" aria-live="polite">Loading available assessments...</section>
      ) : (
        <AssessmentGroups assessments={assessments} />
      )}
    </div>
  );
}

function AssessmentGroups({ assessments }: { assessments: Assessment[] }) {
  const { available, other } = partitionAssessments(assessments);

  return (
    <div className="space-y-8">
      <section aria-labelledby="active-assessments-heading">
        <h2 id="active-assessments-heading" className="mb-4 text-lg font-semibold">Active assessments</h2>
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {available.map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
          {available.length === 0 ? (
            <div className="panel text-sm text-white/55 lg:col-span-2 xl:col-span-3">No assessments are available to start or continue right now.</div>
          ) : null}
        </div>
      </section>
      <section aria-labelledby="other-assessments-heading">
        <h2 id="other-assessments-heading" className="mb-4 text-lg font-semibold">Other assessments</h2>
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {other.map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
          {other.length === 0 ? (
            <div className="panel text-sm text-white/55 lg:col-span-2 xl:col-span-3">No scheduled or expired assessments.</div>
          ) : null}
        </div>
      </section>
    </div>
  );
}
