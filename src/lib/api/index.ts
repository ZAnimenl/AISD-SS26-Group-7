import type {
  AdminDashboard,
  AggregateReport,
  AiInteractionType,
  Assessment,
  AssessmentStatus,
  AttemptStatus,
  AdminTestCase,
  Language,
  MockUser,
  Question,
  ReportListItem,
  Role,
  RunResult,
  StudentDashboard,
  SubmissionResult,
  UserAccount,
  WorkspaceState
} from "@/lib/types";

const DEFAULT_API_BASE_URL = "http://localhost:5040/api/v1";
const LOCAL_FALLBACK_API_BASE_URL = "http://localhost:5041/api/v1";
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? DEFAULT_API_BASE_URL;
const API_BASE_URLS = API_BASE_URL === DEFAULT_API_BASE_URL
  ? [DEFAULT_API_BASE_URL, LOCAL_FALLBACK_API_BASE_URL]
  : [API_BASE_URL];
const TOKEN_KEY = "ojsharp.auth.token";
const USER_KEY = "ojsharp.auth.user";

interface ApiResponse<T> {
  ok: boolean;
  data: T | null;
  error: { code: string; message: string } | null;
}

interface ParsedApiResponse<T> {
  payload: ApiResponse<T> | null;
  bodyText: string;
}

interface LoginResponse {
  token: string;
  user: BackendUser;
}

interface BackendUser {
  user_id: string;
  full_name: string;
  email: string;
  role: Role;
  status?: "active" | "inactive";
  created_at?: string;
}

interface BackendAttemptResponse {
  session_id: string;
  assessment_id: string;
  session_status: string;
  started_at: string;
  expires_at: string;
  server_time: string;
}

interface BackendAttempt {
  backend_attempt_id: string;
  assessment_id: string;
  attempt_status: string;
  started_at: string;
  expires_at: string;
  server_time: string;
}

function getToken() {
  if (typeof window === "undefined") {
    return null;
  }

  return window.localStorage.getItem(TOKEN_KEY);
}

async function apiRequest<T>(path: string, init: RequestInit = {}) {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = getToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetchApi(path, init, headers);

  const { payload, bodyText } = await parseApiResponse<T>(response);

  if (!response.ok) {
    throw new Error(
      payload?.error?.message
        ?? getPlainTextError(bodyText)
        ?? `Backend request failed with ${response.status}`
    );
  }

  if (!payload) {
    throw new Error(`Backend returned an invalid response (${response.status}).`);
  }

  if (!payload.ok) {
    throw new Error(payload.error?.message ?? `Backend request failed with ${response.status}`);
  }

  return payload.data as T;
}

async function fetchApi(path: string, init: RequestInit, headers: Headers) {
  for (let index = 0; index < API_BASE_URLS.length; index += 1) {
    const baseUrl = API_BASE_URLS[index];

    try {
      return await fetch(`${baseUrl}${path}`, {
        ...init,
        headers,
        cache: "no-store"
      });
    } catch {
      if (index === API_BASE_URLS.length - 1) {
        throw new Error("Backend is unreachable. Please check that the backend server is running.");
      }
    }
  }

  throw new Error("Backend is unreachable. Please check that the backend server is running.");
}

async function parseApiResponse<T>(response: Response): Promise<ParsedApiResponse<T>> {
  const bodyText = await response.text();

  if (!bodyText.trim()) {
    return { payload: null, bodyText };
  }

  try {
    return { payload: JSON.parse(bodyText) as ApiResponse<T>, bodyText };
  } catch {
    return { payload: null, bodyText };
  }
}

function getPlainTextError(bodyText: string) {
  const message = bodyText.trim();

  if (!message) {
    return null;
  }

  return message.length > 240 ? `${message.slice(0, 240)}...` : message;
}

export function getStoredUser() {
  if (typeof window === "undefined") {
    return null;
  }

  const value = window.localStorage.getItem(USER_KEY);
  return value ? (JSON.parse(value) as MockUser) : null;
}

export async function login(email: string, password: string) {
  const result = await apiRequest<LoginResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password })
  });
  const user = normalizeUser(result.user);
  window.localStorage.setItem(TOKEN_KEY, result.token);
  window.localStorage.setItem(USER_KEY, JSON.stringify(user));
  return user;
}

export async function logout() {
  try {
    await apiRequest<{ logged_out: boolean }>("/auth/logout", { method: "POST" });
  } finally {
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(USER_KEY);
  }
}

export async function getCurrentUser() {
  return normalizeUser(await apiRequest<BackendUser>("/auth/me"));
}

export async function registerStudent(input: { full_name: string; email: string; password: string }) {
  return normalizeUser(await apiRequest<BackendUser>("/auth/register", {
    method: "POST",
    body: JSON.stringify(input)
  }));
}

export async function getAdminUsers() {
  const raw = await apiRequest<BackendUser[]>("/admin/users/");
  return raw.map(normalizeUserAccount);
}

export async function createAdminUser(input: {
  full_name: string;
  email: string;
  password: string;
  role: Role;
  status?: "active" | "inactive";
}) {
  return normalizeUserAccount(await apiRequest<BackendUser>("/admin/users/", {
    method: "POST",
    body: JSON.stringify({
      ...input,
      status: input.status ?? "active"
    })
  }));
}

export async function getStudentDashboard() {
  const raw = await apiRequest<any>("/student/dashboard");
  return {
    summary: {
      available_assessments: raw.summary?.available_assessments ?? 0,
      in_progress_attempts: raw.summary?.in_progress_attempts ?? raw.summary?.in_progress_sessions ?? 0,
      completed_assessments: raw.summary?.completed_assessments ?? 0,
      average_score: Math.round(raw.summary?.average_score ?? 0)
    },
    recent_activity: (raw.recent_activity ?? []).map((item: any) => ({
      label: item.assessment_title ?? "Assessment session",
      detail: item.session_status ?? "updated",
      timestamp: item.started_at ?? item.expires_at ?? ""
    }))
  } satisfies StudentDashboard;
}

export async function getStudentAssessments() {
  const raw = await apiRequest<any[]>("/student/assessments");
  return raw.map(normalizeAssessment);
}

export async function getStudentResults() {
  const raw = await apiRequest<any[]>("/student/results");
  return raw.map((item) =>
    normalizeAssessment({
      assessment_id: item.assessment_id,
      title: item.assessment_title,
      status: "closed",
      session_status: "submitted",
      score: item.max_score ? Math.round((item.score / item.max_score) * 100) : item.score,
      ai_enabled: false
    })
  );
}

export async function getAdminDashboard() {
  return apiRequest<AdminDashboard>("/admin/dashboard");
}

export async function getAdminAssessments() {
  const raw = await apiRequest<any[]>("/admin/assessments");
  return raw.map(normalizeAssessment);
}

export async function createAssessment(input: {
  title: string;
  description: string;
  duration_minutes: number;
  status: AssessmentStatus;
  ai_enabled: boolean;
}) {
  return apiRequest<{ assessment_id: string }>("/admin/assessments", {
    method: "POST",
    body: JSON.stringify(input)
  });
}

export async function getAdminAssessment(assessmentId: string) {
  return normalizeAssessment(await apiRequest<any>(`/admin/assessments/${assessmentId}`));
}

export async function updateAssessment(input: Assessment) {
  return apiRequest<{ assessment_id: string }>(`/admin/assessments/${input.assessment_id}`, {
    method: "PUT",
    body: JSON.stringify({
      title: input.title,
      description: input.description,
      duration_minutes: input.duration_minutes,
      status: input.status,
      ai_enabled: input.ai_enabled
    })
  });
}

export async function createQuestion(assessmentId: string, input: Question) {
  const raw = await apiRequest<any>(`/admin/assessments/${assessmentId}/questions`, {
    method: "POST",
    body: JSON.stringify(toQuestionRequest(input))
  });

  return normalizeQuestion(raw);
}

export async function updateQuestion(input: Question) {
  const raw = await apiRequest<any>(`/admin/questions/${input.question_id}`, {
    method: "PUT",
    body: JSON.stringify(toQuestionRequest(input))
  });

  return normalizeQuestion(raw);
}

export async function deleteQuestion(questionId: string) {
  return apiRequest<{ question_id: string; deleted: boolean }>(`/admin/questions/${questionId}`, {
    method: "DELETE"
  });
}

export async function createTestCase(questionId: string, input: AdminTestCase) {
  const raw = await apiRequest<{ test_case_id: string }>(`/admin/questions/${questionId}/test-cases`, {
    method: "POST",
    body: JSON.stringify(toTestCaseRequest(input))
  });

  return raw.test_case_id;
}

export async function updateTestCase(input: AdminTestCase) {
  return apiRequest<{ test_case_id: string }>(`/admin/test-cases/${input.test_case_id}`, {
    method: "PUT",
    body: JSON.stringify(toTestCaseRequest(input))
  });
}

export async function deleteTestCase(testCaseId: string) {
  return apiRequest<{ test_case_id: string; deleted: boolean }>(`/admin/test-cases/${testCaseId}`, {
    method: "DELETE"
  });
}

export async function getReportList() {
  const raw = await apiRequest<any[]>("/admin/reports");
  return raw.map((item) => ({
    assessment_id: item.assessment_id,
    assessment_title: item.assessment_title,
    average_score: Math.round(item.average_score ?? 0),
    completion_count: item.completion_count ?? 0,
    participant_count: item.participant_count ?? 0,
    ai_interactions: item.ai_interactions ?? 0
  })) satisfies ReportListItem[];
}

export async function getAggregateReport(assessmentId: string) {
  const raw = await apiRequest<any>(`/reports/aggregate/${assessmentId}`);
  return {
    assessment_id: raw.assessment_id,
    assessment_title: raw.assessment_title,
    average_score: Math.round(raw.average_score ?? 0),
    completion_count: raw.completion_count ?? 0,
    participant_count: raw.participant_count ?? 0,
    ai_interactions: raw.ai_interactions ?? 0,
    score_distribution: raw.score_distribution ?? [],
    students: (raw.students ?? []).map((student: any) => ({
      user_id: student.user_id,
      student_name: student.student_name,
      student_email: student.student_email,
      attempt_status: normalizeAttemptStatus(student.attempt_status ?? student.session_status),
      submission_status: student.submission_status ?? "submitted",
      score: student.score ?? 0,
      max_score: student.max_score ?? 0,
      submitted_at: student.submitted_at ?? "",
      ai_usage_summary: student.ai_usage_summary ?? { total_interactions: 0, main_semantic_tags: [] }
    }))
  } satisfies AggregateReport;
}

export async function getAssessment(assessmentId: string) {
  const assessments = await getStudentAssessments();
  return assessments.find((assessment) => assessment.assessment_id === assessmentId) ?? assessments[0];
}

export async function startAssessment(assessmentId: string) {
  const response = await apiRequest<BackendAttemptResponse>("/sessions/initiate", {
    method: "POST",
    body: JSON.stringify({ assessment_id: assessmentId })
  });
  return normalizeBackendAttempt(response);
}

export async function getAssessmentAttempt(backendAttemptId: string) {
  return normalizeBackendAttempt(await apiRequest<BackendAttemptResponse>(`/sessions/${backendAttemptId}`));
}

export async function getWorkspaceContext(assessmentId: string, backendAttemptId: string) {
  // Backend gap: current backend context endpoint still requires a sessionId query value.
  // Keep this transient ID in memory only; do not persist it as frontend-owned assessment state.
  const query = new URLSearchParams({ sessionId: backendAttemptId });
  return normalizeAssessment(await apiRequest<any>(`/assessments/${assessmentId}/context?${query}`));
}

export async function getWorkspace(backendAttemptId: string) {
  return apiRequest<WorkspaceState>(`/sessions/${backendAttemptId}/workspace`);
}

export async function autosaveWorkspace(
  backendAttemptId: string,
  questionId: string,
  selectedLanguage: Language,
  activeFile: string,
  content: string,
  version?: number
) {
  return apiRequest<WorkspaceState>(`/sessions/${backendAttemptId}/workspace`, {
    method: "PUT",
    body: JSON.stringify({
      questions: {
        [questionId]: {
          selected_language: selectedLanguage,
          active_file: activeFile,
          files: {
            [activeFile]: {
              language: selectedLanguage,
              content
            }
          },
          version
        }
      }
    })
  });
}

export async function runCode(input: {
  backend_attempt_id: string;
  assessment_id: string;
  question_id: string;
  selected_language: Language;
  active_file_content: string;
}) {
  return apiRequest<RunResult>("/executions/run", {
    method: "POST",
    body: JSON.stringify({
      session_id: input.backend_attempt_id,
      assessment_id: input.assessment_id,
      question_id: input.question_id,
      selected_language: input.selected_language,
      active_file_content: input.active_file_content
    })
  });
}

export async function finalizeSubmission(backendAttemptId: string) {
  return apiRequest<SubmissionResult>("/submissions/finalize", {
    method: "POST",
    body: JSON.stringify({ session_id: backendAttemptId })
  });
}

export async function getAiResponse(input: {
  backend_attempt_id: string;
  assessment_id: string;
  question_id: string;
  interaction_type: AiInteractionType;
  message: string;
  selected_language: Language;
  active_file_content: string;
}) {
  const response = await apiRequest<{ response_markdown: string }>("/ai/chat", {
    method: "POST",
    body: JSON.stringify({
      session_id: input.backend_attempt_id,
      assessment_id: input.assessment_id,
      question_id: input.question_id,
      interaction_type: input.interaction_type,
      message: input.message,
      selected_language: input.selected_language,
      active_file_content: input.active_file_content
    })
  });
  return response.response_markdown;
}

function normalizeUser(user: BackendUser): MockUser {
  const rawUser = user as BackendUser & {
    Role?: Role;
    Status?: "active" | "inactive";
    Email?: string;
    FullName?: string;
  };

  return {
    user_id: user.user_id,
    name: user.full_name ?? rawUser.FullName,
    full_name: user.full_name ?? rawUser.FullName,
    email: user.email ?? rawUser.Email,
    role: normalizeRole(user.role ?? rawUser.Role),
    status: user.status ?? rawUser.Status,
    created_at: user.created_at
  };
}

function normalizeUserAccount(user: BackendUser): UserAccount {
  const rawUser = user as BackendUser & {
    Role?: Role;
    Status?: "active" | "inactive";
    Email?: string;
    FullName?: string;
  };

  return {
    user_id: user.user_id,
    full_name: user.full_name ?? rawUser.FullName ?? "",
    email: user.email ?? rawUser.Email ?? "",
    role: normalizeRole(user.role ?? rawUser.Role),
    status: user.status ?? rawUser.Status ?? "active",
    created_at: user.created_at ?? ""
  };
}

function normalizeRole(role: string | undefined): Role {
  return role === "administrator" ? "administrator" : "student";
}

function normalizeAssessment(raw: any): Assessment {
  return {
    assessment_id: raw.assessment_id,
    title: raw.title ?? raw.assessment_title ?? "Assessment",
    description: raw.description ?? "",
    duration_minutes: raw.duration_minutes ?? 0,
    status: (raw.status ?? "active") as AssessmentStatus,
    ai_enabled: Boolean(raw.ai_enabled),
    closes_at: raw.closes_at ?? raw.expires_at ?? new Date().toISOString(),
    question_count: raw.question_count ?? raw.questions?.length ?? 0,
    attempt_status: normalizeAttemptStatus(raw.attempt_status ?? raw.session_status),
    progress_percent: raw.session_status === "active" ? 25 : raw.session_status === "submitted" ? 100 : 0,
    score: raw.score,
    questions: (raw.questions ?? []).map(normalizeQuestion)
  };
}

function normalizeQuestion(question: any): Question {
  return {
    question_id: question.question_id,
    title: question.title ?? "",
    problem_description_markdown: question.problem_description_markdown ?? "",
    admin_notes: question.admin_notes ?? null,
    sort_order: question.sort_order ?? 0,
    max_score: question.max_score ?? 100,
    constraints: question.constraints ?? [],
    language_constraints: question.language_constraints ?? ["python", "javascript"],
    starter_code: {
      python: question.starter_code?.python ?? "",
      javascript: question.starter_code?.javascript ?? ""
    },
    public_examples: question.public_examples ?? [],
    admin_test_cases: (question.admin_test_cases ?? []).map(normalizeAdminTestCase)
  };
}

function normalizeAdminTestCase(testCase: any): AdminTestCase {
  const input = testCase.input ?? testCase.input_preview ?? "";
  const expectedOutput = testCase.expected_output ?? testCase.expected_output_preview ?? "";

  return {
    test_case_id: testCase.test_case_id,
    name: testCase.name ?? "",
    visibility: testCase.visibility === "hidden" ? "hidden" : "public",
    input,
    expected_output: expectedOutput,
    input_preview: input,
    expected_output_preview: expectedOutput
  };
}

function toQuestionRequest(question: Question) {
  return {
    title: question.title,
    problem_description_markdown: question.problem_description_markdown,
    language_constraints: question.language_constraints.length ? question.language_constraints : ["python", "javascript"],
    starter_code: {
      python: question.starter_code.python,
      javascript: question.starter_code.javascript
    },
    admin_notes: question.admin_notes ?? "",
    sort_order: question.sort_order ?? 0,
    max_score: question.max_score ?? 100
  };
}

function toTestCaseRequest(testCase: AdminTestCase) {
  return {
    name: testCase.name,
    visibility: testCase.visibility,
    input: testCase.input ?? testCase.input_preview,
    expected_output: testCase.expected_output ?? testCase.expected_output_preview
  };
}

function normalizeAttemptStatus(value: string | undefined): AttemptStatus {
  if (value === "active" || value === "expired" || value === "submitted" || value === "closed") {
    return value;
  }

  return "not_started";
}

function normalizeBackendAttempt(response: BackendAttemptResponse): BackendAttempt {
  return {
    backend_attempt_id: response.session_id,
    assessment_id: response.assessment_id,
    attempt_status: response.session_status,
    started_at: response.started_at,
    expires_at: response.expires_at,
    server_time: response.server_time
  };
}
