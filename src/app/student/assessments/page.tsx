import { AssessmentCard } from "@/components/student/AssessmentCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { getStudentAssessments } from "@/lib/mock-api";

export default function StudentAssessmentsPage() {
  const assessments = getStudentAssessments();

  return (
    <div>
      <SectionHeader eyebrow="Student" title="Assessments" />
      <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
        {assessments.map((assessment) => <AssessmentCard key={assessment.assessment_id} assessment={assessment} />)}
      </div>
    </div>
  );
}
