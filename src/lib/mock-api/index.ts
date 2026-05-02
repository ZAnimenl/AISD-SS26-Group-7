import { mockUsers } from "@/mocks/auth.mock";
import { mockStudentDashboard } from "@/mocks/student-dashboard.mock";
import { mockAdminDashboard } from "@/mocks/admin-dashboard.mock";
import { mockAssessments } from "@/mocks/assessments.mock";
import { mockWorkspace, mockRunResult, mockSubmissionResult } from "@/mocks/workspace.mock";
import { mockAiResponses } from "@/mocks/ai.mock";
import { mockAggregateReports, mockReportList } from "@/mocks/reports.mock";
import type { AiInteractionType, Language, Role } from "@/lib/types";

export function getMockUser(role: Role) {
  // TODO(API): GET /api/v1/auth/me
  // Purpose: Load the authenticated user and role from secure backend auth context.
  // Current MVP behavior: return selected mock role only.
  return mockUsers.find((user) => user.role === role) ?? mockUsers[0];
}

export function mockLogin(role: Role) {
  // TODO(API): POST /api/v1/auth/login
  // Purpose: Authenticate user and redirect by role.
  // Current MVP behavior: role selection only; no real JWT or session_id is created.
  return getMockUser(role);
}

export function mockLogout() {
  // TODO(API): POST /api/v1/auth/logout
  // Purpose: End authenticated backend session.
  // Current MVP behavior: navigation-only logout affordance.
  return true;
}

export function getStudentDashboard() {
  // TODO(API): GET /api/v1/student/dashboard
  // Purpose: Load student dashboard summary cards and recent activity.
  return mockStudentDashboard;
}

export function getStudentAssessments() {
  // TODO(API): GET /api/v1/student/assessments
  // Purpose: Load authenticated student's available, active, submitted, and closed assessments.
  return mockAssessments;
}

export function getStudentResults() {
  // TODO(API): GET /api/v1/student/results
  // Purpose: Load completed assessments and scores for authenticated student.
  return mockAssessments.filter((assessment) => assessment.attempt_status === "submitted");
}

export function getAssessment(assessmentId: string) {
  return mockAssessments.find((assessment) => assessment.assessment_id === assessmentId) ?? mockAssessments[0];
}

export function startAssessment(assessmentId: string) {
  // TODO(API): POST /api/v1/student/assessments/{assessment_id}/start
  // Purpose: Start or resume the authenticated user's assessment attempt.
  // Backend derives active attempt from authenticated user + assessment_id.
  // Current MVP behavior: return mock active attempt state only.
  return {
    assessment_id: assessmentId,
    attempt_status: "active",
    started_at: "2026-05-02T12:00:00Z",
    expires_at: "2026-05-02T13:30:00Z",
    server_time: "2026-05-02T12:22:00Z"
  };
}

export function getWorkspaceContext(assessmentId: string) {
  // TODO(API): GET /api/v1/student/assessments/{assessment_id}/context
  // Purpose: Load student-safe assessment context. Must never include hidden test input/output.
  return getAssessment(assessmentId);
}

export function getAssessmentAttempt(assessmentId: string) {
  // TODO(API): GET /api/v1/student/assessments/{assessment_id}/attempt
  // Purpose: Load timer and active attempt state. Backend resolves attempt from auth context.
  return startAssessment(assessmentId);
}

export function getWorkspace(assessmentId: string) {
  // TODO(API): GET /api/v1/student/assessments/{assessment_id}/workspace
  // Purpose: Restore authenticated user's saved code for the active assessment.
  return { ...mockWorkspace, assessment_id: assessmentId };
}

export function autosaveWorkspace() {
  // TODO(API): PUT /api/v1/student/assessments/{assessment_id}/workspace
  // Purpose: Debounced autosave for editor content.
  // Save unit: authenticated user + assessment + question.
  // Current MVP behavior: local mock state only.
  return { ok: true, saved_at: new Date().toISOString() };
}

export function runCodeMock() {
  // TODO(API): POST /api/v1/executions/run
  // Purpose: Send current code, language, assessment_id, and question_id.
  // Backend derives user and active attempt from auth context.
  // Current MVP behavior: use mock response only.
  return mockRunResult;
}

export function finalizeSubmissionMock() {
  // TODO(API): POST /api/v1/submissions/finalize
  // Purpose: Submit final code, language, assessment_id, and question_id.
  // Backend derives user and active attempt from auth context.
  // Current MVP behavior: use mock result only.
  return mockSubmissionResult;
}

export function getAiResponse(type: AiInteractionType, _language: Language, _content: string) {
  // TODO(API): POST /api/v1/ai/chat
  // Purpose: Send assessment_id, question_id, interaction_type, message, selected_language, and active_file_content.
  // Backend derives user and active attempt from auth context.
  // Current MVP behavior: mock AI response only.
  return mockAiResponses[type];
}

export function getAdminDashboard() {
  // TODO(API): GET /api/v1/admin/dashboard
  // Purpose: Load admin dashboard summary cards and recent activity.
  return mockAdminDashboard;
}

export function getAdminAssessments() {
  // TODO(API): GET /api/v1/admin/assessments
  // Purpose: Load admin assessment list.
  return mockAssessments;
}

export function createAssessmentMock() {
  // TODO(API): POST /api/v1/admin/assessments
  // Purpose: Create assessment from admin form fields.
  // Current MVP behavior: form is local only.
  return { ok: true };
}

export function updateAssessmentMock(assessmentId: string) {
  // TODO(API): GET /api/v1/admin/assessments/{assessment_id}
  // TODO(API): PUT /api/v1/admin/assessments/{assessment_id}
  // TODO(API): POST /api/v1/admin/assessments/{assessment_id}/questions
  // TODO(API): PUT /api/v1/admin/questions/{question_id}
  // TODO(API): GET /api/v1/admin/questions/{question_id}/test-cases
  // TODO(API): POST /api/v1/admin/questions/{question_id}/test-cases
  // Purpose: Replace local editor state with admin assessment/question/test-case APIs.
  return getAssessment(assessmentId);
}

export function getReportList() {
  // TODO(API): GET /api/v1/admin/reports
  // Purpose: Load report list by assessment.
  return mockReportList;
}

export function getAggregateReport(assessmentId: string) {
  // TODO(API): GET /api/v1/reports/aggregate/{assessment_id}
  // Purpose: Load aggregate report metrics, score distribution, student results, and AI usage summaries.
  return mockAggregateReports[assessmentId] ?? mockAggregateReports["algorithms-2026"];
}
