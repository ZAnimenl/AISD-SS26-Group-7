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

  useEffect(() => {
    getAdminAssessments().then(setAssessments).catch((exception) => {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to load assessments.");
    });
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
      <AdminAssessmentTable assessments={assessments} />
    </div>
  );
}
