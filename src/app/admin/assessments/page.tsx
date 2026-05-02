"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";
import { AdminAssessmentTable } from "@/components/admin/AdminAssessmentTable";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getAdminAssessments } from "@/lib/api";
import type { Assessment } from "@/lib/types";

export default function AdminAssessmentsPage() {
  const router = useRouter();
  const [assessments, setAssessments] = useState<Assessment[]>([]);

  useEffect(() => {
    getAdminAssessments().then(setAssessments).catch(() => router.replace("/login"));
  }, [router]);

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
