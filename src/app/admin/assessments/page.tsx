"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";
import { AdminAssessmentTable } from "@/components/admin/AdminAssessmentTable";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getAdminAssessments, isAuthenticationError } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function AdminAssessmentsPage() {
  const router = useRouter();
  const [assessments, setAssessments] = useState<Assessment[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    getAdminAssessments()
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
      })
      .finally(() => setIsLoading(false));
  }, [router]);

  if (error) {
    return <SectionHeader eyebrow="Administrator" title={error} />;
  }

  return (
    <div>
      <SectionHeader
        eyebrow="Administrator"
        title="Assessments"
        action={<Link className="btn-primary" href="/admin/assessments/new"><Plus size={16} /> New assessment</Link>}
      />
      {isLoading ? (
        <section className="panel text-sm text-white/55" aria-live="polite">Loading assessments from backend...</section>
      ) : (
        <AdminAssessmentTable assessments={assessments} />
      )}
    </div>
  );
}
