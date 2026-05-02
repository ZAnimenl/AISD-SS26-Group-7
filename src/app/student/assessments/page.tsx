"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AssessmentCard } from "@/components/student/AssessmentCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getStudentAssessments } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function StudentAssessmentsPage() {
  const router = useRouter();
  const [assessments, setAssessments] = useState<Assessment[] | null>(null);

  useEffect(() => {
    getStudentAssessments().then(setAssessments).catch(() => router.replace("/login"));
  }, [router]);

  return (
    <div>
      <SectionHeader eyebrow="Student" title="Assessments" />
      <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
        {(assessments ?? []).map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
      </div>
    </div>
  );
}
