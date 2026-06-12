"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";
import { AdminAssessmentTable } from "@/components/admin/AdminAssessmentTable";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { deleteAssessment, getAdminAssessments, isAuthenticationError } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function AdminAssessmentsPage() {
  const router = useRouter();
  const [assessments, setAssessments] = useState<Assessment[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [deletingAssessmentId, setDeletingAssessmentId] = useState<string | null>(null);

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

  async function handleDeleteAssessment(assessment: Assessment) {
    if (!window.confirm(`Delete "${assessment.title}" and all of its questions, attempts, submissions, and AI usage records?`)) {
      return;
    }

    setActionError(null);
    setDeletingAssessmentId(assessment.assessment_id);
    try {
      await deleteAssessment(assessment.assessment_id);
      setAssessments((currentAssessments) =>
        currentAssessments.filter((currentAssessment) => currentAssessment.assessment_id !== assessment.assessment_id)
      );
    } catch (exception) {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setActionError(exception instanceof Error ? exception.message : "Unable to delete assessment.");
    } finally {
      setDeletingAssessmentId(null);
    }
  }

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
      {actionError ? <section className="panel mb-4 text-sm text-pinkGlow">{actionError}</section> : null}
      {isLoading ? (
        <section className="panel text-sm text-white/55" aria-live="polite">Loading assessments from backend...</section>
      ) : (
        <AdminAssessmentTable assessments={assessments} deletingAssessmentId={deletingAssessmentId} onDeleteAssessment={handleDeleteAssessment} />
      )}
    </div>
  );
}
