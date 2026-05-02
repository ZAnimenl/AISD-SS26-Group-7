import { WorkspaceClient } from "@/components/workspace/WorkspaceClient";
import { getAssessmentAttempt, getWorkspace, getWorkspaceContext } from "@/lib/mock-api";

export default function WorkspacePage({ params }: { params: { assessmentId: string } }) {
  const assessment = getWorkspaceContext(params.assessmentId);
  getAssessmentAttempt(params.assessmentId);
  const workspace = getWorkspace(params.assessmentId);

  return <WorkspaceClient assessment={assessment} workspace={workspace} />;
}
