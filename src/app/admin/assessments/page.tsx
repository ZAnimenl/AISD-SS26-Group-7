import Link from "next/link";
import { Plus } from "lucide-react";
import { AdminAssessmentTable } from "@/components/admin/AdminAssessmentTable";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getAdminAssessments } from "@/lib/mock-api";

export default function AdminAssessmentsPage() {
  return (
    <div>
      <SectionHeader
        eyebrow="Administrator"
        title="Assessments"
        action={<Link className="btn-primary" href="/admin/assessments/new"><Plus size={16} /> New assessment</Link>}
      />
      <AdminAssessmentTable assessments={getAdminAssessments()} />
    </div>
  );
}
