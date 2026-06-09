import type {
  AdminDashboard,
  AggregateReport,
  AiAssistantResponse,
  AiInteractionType,
  AiUsageSummary,
  Assessment,
  AssessmentStatus,
  AttemptStatus,
  AuthUser,
  AdminTestCase,
  Difficulty,
  Language,
  Question,
  ReportListItem,
  Role,
  RunResult,
  StudentDashboard,
  SubmissionResult,
  SystemConfig,
  TaskType,
  TokenEfficiencyIndicator,
  UserAccount,
  VerificationMode,
  WorkspaceFile,
  WorkspaceQuestionState,
  WorkspaceState
} from "@/lib/types";
import { normalizeTestCode } from "@/lib/languages";
import {
  clearStoredAuth,
  getStoredToken,
  storeAuth
} from "@/lib/api/authStorage";

export {
  clearStoredAuth,
  getStoredUser,
  hasStoredAuth
} from "@/lib/api/authStorage";

const DEFAULT_API_BASE_URL = "http://localhost:5140/api/v1";
const LOCAL_FALLBACK_API_BASE_URLS = [
  "http://localhost:5141/api/v1",
  "http://localhost:5040/api/v1",
  "http://localhost:5041/api/v1"
];
const CONFIGURED_API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();
const IS_PRODUCTION_BUILD = process.env.NODE_ENV === "production";
const API_BASE_URLS = CONFIGURED_API_BASE_URL
  ? [CONFIGURED_API_BASE_URL]
  : IS_PRODUCTION_BUILD
    ? []
    : [DEFAULT_API_BASE_URL, ...LOCAL_FALLBACK_API_BASE_URLS];
const API_BASE_KEY = "ojsharp.api.base_url";
const startAssessmentRequests = new Map<string, Promise<void>>();

interface ApiResponse<T> {
  ok: boolean;
  data: T | null;
  error: { code: string; message: string } | null;
}

interface ParsedApiResponse<T> {
  payload: ApiResponse<T> | null;
  bodyText: string;
}

export class ApiRequestError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string
  ) {
    super(message);
    this.name = "ApiRequestError";
  }
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

async function apiRequest<T>(path: string, init: RequestInit = {}) {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = getStoredToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetchApi(path, init, headers);

  const { payload, bodyText } = await parseApiResponse<T>(response);

  if (!response.ok) {
    if (response.status === 401) {
      clearStoredAuth();
    }

    throw new ApiRequestError(
      payload?.error?.message
        ?? getPlainTextError(bodyText)
        ?? `Backend request failed with ${response.status}`,
      response.status,
      payload?.error?.code
    );
  }

  if (!payload) {
    throw new ApiRequestError(`Backend returned an invalid response (${response.status}).`, response.status, "INVALID_RESPONSE");
  }

  if (!payload.ok) {
    throw new ApiRequestError(
      payload.error?.message ?? `Backend request failed with ${response.status}`,
      response.status,
      payload.error?.code
    );
  }

  return payload.data as T;
}

export function isAuthenticationError(exception: unknown) {
  return exception instanceof ApiRequestError && (exception.status === 401 || exception.status === 403);
}

async function fetchApi(path: string, init: RequestInit, headers: Headers) {
  const baseUrls = getApiBaseUrlOrder();

  for (let index = 0; index < baseUrls.length; index += 1) {
    const baseUrl = baseUrls[index];

    try {
      const response = await fetch(`${baseUrl}${path}`, {
        ...init,
        headers,
        cache: "no-store"
      });

      rememberApiBaseUrl(baseUrl);
      return response;
    } catch {
      if (index === baseUrls.length - 1) {
        throw new Error("Backend is unreachable. Please check that the backend server is running.");
      }
    }
  }

  throw new Error("Backend is unreachable. Please check that the backend server is running.");
}

function getApiBaseUrlOrder() {
  if (API_BASE_URLS.length === 0) {
    throw new Error("NEXT_PUBLIC_API_BASE_URL must be configured for production frontend deployments.");
  }

  if (typeof window === "undefined") {
    return API_BASE_URLS;
  }

  const rememberedBaseUrl = window.localStorage.getItem(API_BASE_KEY);
  if (!rememberedBaseUrl || !API_BASE_URLS.includes(rememberedBaseUrl)) {
    return API_BASE_URLS;
  }

  return [rememberedBaseUrl, ...API_BASE_URLS.filter((baseUrl) => baseUrl !== rememberedBaseUrl)];
}

function rememberApiBaseUrl(baseUrl: string) {
  if (typeof window !== "undefined") {
    window.localStorage.setItem(API_BASE_KEY, baseUrl);
  }
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

export async function login(email: string, password: string) {
  const result = await apiRequest<LoginResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password })
  });
  const user = normalizeUser(result.user);
  storeAuth(result.token, user);
  return user;
}

export async function logout() {
  try {
    await apiRequest<{ logged_out: boolean }>("/auth/logout", { method: "POST" });
  } finally {
    clearStoredAuth();
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

export async function getSystemConfig() {
  return apiRequest<SystemConfig>("/config");
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
      in_progress_attempts: raw.summary?.in_progress_attempts ?? 0,
      completed_assessments: raw.summary?.completed_assessments ?? 0,
      average_score: Math.round(raw.summary?.average_score ?? 0)
    },
    recent_activity: (raw.recent_activity ?? []).map((item: any) => ({
      label: item.assessment_title ?? "Assessment attempt",
      detail: item.attempt_status ?? "updated",
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
  return raw.map((item) => {
    const normalized = normalizeAssessment({
      assessment_id: item.assessment_id,
      title: item.assessment_title,
      status: "closed",
      attempt_status: "submitted",
      score: item.max_score ? Math.round((item.score / item.max_score) * 100) : item.score,
      question_count: item.question_count,
      ai_enabled: false
    });
    normalized.submission_id = item.submission_id;
    return normalized;
  });
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
  shared_prototype_reference?: string | null;
  shared_prototype_version?: string | null;
  shared_prototype_metadata?: Record<string, string>;
}) {
  return apiRequest<{ assessment_id: string }>("/admin/assessments", {
    method: "POST",
    body: JSON.stringify(input)
  });
}

export async function generateAssessment(input: {
  title: string;
  description: string;
  duration_minutes: number;
  status: AssessmentStatus;
  ai_enabled: boolean;
  shared_prototype_reference?: string | null;
  shared_prototype_version?: string | null;
  shared_prototype_metadata?: Record<string, string>;
}) {
  return apiRequest<{ assessment_id: string }>("/admin/assessments/generate", {
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
      ai_enabled: input.ai_enabled,
      shared_prototype_reference: input.shared_prototype_reference ?? null,
      shared_prototype_version: input.shared_prototype_version ?? null,
      shared_prototype_metadata: input.shared_prototype_metadata ?? {}
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

export async function generateQuestionDraft(assessmentId: string, input: {
  task_type: TaskType;
  difficulty: Difficulty;
  supported_languages: Language[];
  starter_prototype_reference?: string | null;
}) {
  const raw = await apiRequest<any>(`/admin/assessments/${assessmentId}/questions/generate`, {
    method: "POST",
    body: JSON.stringify(input)
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
    ai_interactions: item.ai_interactions ?? 0,
    total_ai_tokens: item.total_ai_tokens ?? item.ai_usage_summary?.total_tokens ?? 0
  })) satisfies ReportListItem[];
}

export async function getAggregateReport(assessmentId: string) {
  const raw = await apiRequest<any>(`/reports/aggregate/${assessmentId}`);
  const aiUsageSummary = normalizeAiUsageSummary(raw.ai_usage_summary);
  return {
    assessment_id: raw.assessment_id,
    assessment_title: raw.assessment_title,
    average_score: Math.round(raw.average_score ?? 0),
    completion_count: raw.completion_count ?? 0,
    participant_count: raw.participant_count ?? 0,
    ai_interactions: raw.ai_interactions ?? 0,
    total_ai_tokens: raw.total_ai_tokens ?? aiUsageSummary.total_tokens,
    ai_usage_summary: aiUsageSummary,
    score_distribution: raw.score_distribution ?? [],
    students: (raw.students ?? []).map((student: any) => ({
      user_id: student.user_id,
      student_name: student.student_name,
      student_email: student.student_email,
      attempt_status: normalizeAttemptStatus(student.attempt_status),
      submission_status: student.submission_status ?? "not_submitted",
      score: student.score ?? 0,
      max_score: student.max_score ?? 0,
      submitted_at: student.submitted_at ?? "",
      ai_usage_summary: normalizeAiUsageSummary(student.ai_usage_summary)
    }))
  } satisfies AggregateReport;
}

export async function getAssessment(assessmentId: string) {
  const assessments = await getStudentAssessments();
  const assessment = assessments.find((item) => item.assessment_id === assessmentId);
  if (!assessment) {
    throw new ApiRequestError("Assessment was not found.", 404, "ASSESSMENT_NOT_FOUND");
  }

  return assessment;
}

export async function startAssessment(assessmentId: string) {
  const existingRequest = startAssessmentRequests.get(assessmentId);
  if (existingRequest) {
    return existingRequest;
  }

  const request = apiRequest<unknown>(`/assessments/${assessmentId}/attempts/start`, {
    method: "POST"
  }).then(() => undefined)
    .finally(() => {
      startAssessmentRequests.delete(assessmentId);
    });

  startAssessmentRequests.set(assessmentId, request);
  return request;
}

export async function getWorkspaceContext(assessmentId: string) {
  return normalizeAssessment(await apiRequest<any>(`/assessments/${assessmentId}/context`));
}

export async function getWorkspace(assessmentId: string) {
  return normalizeWorkspace(await apiRequest<any>(`/assessments/${assessmentId}/workspace`), assessmentId);
}

export async function autosaveWorkspace(
  assessmentId: string,
  questionId: string,
  selectedLanguage: Language,
  activeFile: string,
  content: string,
  version?: number
) {
  return saveWorkspace(assessmentId, {
    [questionId]: {
      selected_language: selectedLanguage,
      active_file: activeFile,
      files: {
        [activeFile]: {
          language: selectedLanguage,
          content
        }
      },
      last_saved_at: "",
      version: version ?? 0
    }
  });
}

export async function saveWorkspace(
  assessmentId: string,
  questions: Record<string, WorkspaceQuestionState>
) {
  return apiRequest<WorkspaceState>(`/assessments/${assessmentId}/workspace`, {
    method: "PUT",
    body: JSON.stringify({
      questions: Object.fromEntries(Object.entries(questions).map(([questionId, state]) => [
        questionId,
        {
          selected_language: state.selected_language,
          active_file: state.active_file,
          files: state.files,
          version: state.version
        }
      ]))
    })
  });
}

export async function runCode(input: {
  assessment_id: string;
  question_id: string;
  selected_language: Language;
  files: Record<string, string>;
}) {
  return apiRequest<RunResult>(`/assessments/${input.assessment_id}/questions/${input.question_id}/run`, {
    method: "POST",
    body: JSON.stringify({
      selected_language: input.selected_language,
      files: input.files
    })
  });
}

export async function finalizeSubmission(assessmentId: string) {
  return apiRequest<SubmissionResult>(`/assessments/${assessmentId}/submit`, {
    method: "POST"
  });
}

export async function getAiResponse(input: {
  assessment_id: string;
  question_id: string;
  interaction_type: AiInteractionType;
  message: string;
  selected_language: Language;
  active_file_content: string;
  active_file_name: string;
  visible_files: Record<string, string>;
  last_run_result?: RunResult | null;
}) {
  const response = await apiRequest<AiAssistantResponse>(`/assessments/${input.assessment_id}/questions/${input.question_id}/ai/assist`, {
    method: "POST",
    body: JSON.stringify({
      interaction_type: input.interaction_type,
      message: input.message,
      selected_language: input.selected_language,
      active_file_content: input.active_file_content,
      active_file_name: input.active_file_name,
      visible_files: input.visible_files,
      last_run_result: input.last_run_result
        ? {
          status: input.last_run_result.status,
          stdout: input.last_run_result.stdout,
          stderr: input.last_run_result.stderr,
          test_results: input.last_run_result.test_results.map((test) => ({
            name: test.name,
            passed: test.passed,
            output: test.output
          }))
        }
        : null
    })
  });
  return response;
}

export async function getAiUsage(assessmentId: string) {
  return apiRequest<{
    total_interactions: number;
    total_input_tokens: number;
    total_output_tokens: number;
    total_tokens: number;
    average_tokens_per_interaction: number;
    by_type: Record<string, number>;
  }>(`/assessments/${assessmentId}/ai-usage`);
}

function normalizeUser(user: BackendUser): AuthUser {
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
  const attemptStatus = raw.attempt_status;

  return {
    assessment_id: raw.assessment_id,
    title: raw.title ?? raw.assessment_title ?? "Assessment",
    description: raw.description ?? "",
    duration_minutes: raw.duration_minutes ?? 0,
    status: (raw.status ?? "active") as AssessmentStatus,
    ai_enabled: Boolean(raw.ai_enabled),
    shared_prototype_reference: raw.shared_prototype_reference ?? null,
    shared_prototype_version: raw.shared_prototype_version ?? null,
    shared_prototype_metadata: normalizeMetadata(raw.shared_prototype_metadata),
    supported_task_categories: (raw.supported_task_categories ?? []).map(normalizeTaskType),
    supported_verification_modes: (raw.supported_verification_modes ?? []).map((mode: string | undefined) => normalizeVerificationMode(mode)),
    closes_at: raw.closes_at ?? raw.expires_at ?? new Date().toISOString(),
    question_count: raw.question_count ?? raw.questions?.length ?? 0,
    attempt_status: normalizeAttemptStatus(attemptStatus),
    progress_percent: attemptStatus === "active" ? 25 : attemptStatus === "submitted" ? 100 : 0,
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
    task_type: normalizeTaskType(question.task_type),
    difficulty: normalizeDifficulty(question.difficulty),
    verification_mode: normalizeVerificationMode(question.verification_mode, question.task_type),
    starter_prototype_reference: question.starter_prototype_reference ?? null,
    sort_order: question.sort_order ?? 0,
    max_score: question.max_score ?? 100,
    constraints: question.constraints ?? [],
    language_constraints: normalizeStudentLanguageConstraints(question.language_constraints),
    starter_code: {
      python: question.starter_code?.python ?? {},
      javascript: question.starter_code?.javascript ?? {},
      typescript: question.starter_code?.typescript ?? {}
    },
    starter_files_metadata: normalizeNestedMetadata(question.starter_files_metadata),
    verification_metadata: normalizeMetadata(question.verification_metadata),
    grading_configuration: normalizeMetadata(question.grading_configuration),
    authoring_source: normalizeAuthoringSource(question.authoring_source),
    traceability_metadata: normalizeMetadata(question.traceability_metadata),
    public_examples: question.public_examples ?? [],
    admin_test_cases: (question.admin_test_cases ?? []).map(normalizeAdminTestCase)
  };
}

function normalizeWorkspace(raw: any, assessmentId: string): WorkspaceState {
  const rawQuestions = raw?.questions && typeof raw.questions === "object" && !Array.isArray(raw.questions)
    ? raw.questions as Record<string, any>
    : {};

  const questions = Object.fromEntries(
    Object.entries(rawQuestions).map(([questionId, state]) => {
      const files = normalizeWorkspaceFiles(state?.files);
      const selectedLanguage = normalizeWorkspaceLanguage(state?.selected_language);
      const activeFile = typeof state?.active_file === "string" && state.active_file.trim()
        ? state.active_file
        : Object.keys(files)[0] ?? (selectedLanguage === "javascript" ? "main.js" : "main.py");

      return [
        questionId,
        {
          selected_language: selectedLanguage,
          active_file: activeFile,
          files: Object.keys(files).length
            ? files
            : { [activeFile]: { language: selectedLanguage, content: "" } },
          last_saved_at: typeof state?.last_saved_at === "string" ? state.last_saved_at : "",
          version: typeof state?.version === "number" ? state.version : 0
        }
      ];
    })
  );

  return {
    assessment_id: typeof raw?.assessment_id === "string" ? raw.assessment_id : assessmentId,
    questions
  };
}

function normalizeWorkspaceFiles(value: unknown): Record<string, WorkspaceFile> {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(value as Record<string, any>).map(([fileName, file]) => [
      fileName,
      {
        language: normalizeWorkspaceLanguage(file?.language),
        content: typeof file?.content === "string" ? file.content : ""
      }
    ])
  );
}

function normalizeWorkspaceLanguage(value: string | undefined): Language {
  return value === "javascript" ? "javascript" : "python";
}

function normalizeAdminTestCase(testCase: any): AdminTestCase {
  return {
    test_case_id: testCase.test_case_id,
    name: testCase.name ?? "",
    visibility: testCase.visibility === "hidden" ? "hidden" : "public",
    test_code: normalizeTestCode(testCase.test_code),
    authoring_source: normalizeAuthoringSource(testCase.authoring_source),
    public_metadata: normalizeMetadata(testCase.public_metadata),
    admin_metadata: normalizeMetadata(testCase.admin_metadata),
    traceability_metadata: normalizeMetadata(testCase.traceability_metadata)
  };
}

function normalizeAiUsageSummary(value: any): AiUsageSummary {
  const perTaskTokenTotals = Array.isArray(value?.per_task_token_totals)
    ? value.per_task_token_totals.map((item: any) => ({
      question_id: item.question_id ?? "",
      task_title: item.task_title ?? "Unknown task",
      task_type: item.task_type ?? "",
      interaction_count: item.interaction_count ?? 0,
      total_input_tokens: item.total_input_tokens ?? 0,
      total_output_tokens: item.total_output_tokens ?? 0,
      total_tokens: item.total_tokens ?? 0
    }))
    : [];

  return {
    total_interactions: value?.total_interactions ?? 0,
    total_tokens: value?.total_tokens ?? 0,
    total_input_tokens: value?.total_input_tokens ?? 0,
    total_output_tokens: value?.total_output_tokens ?? 0,
    average_tokens_per_interaction: value?.average_tokens_per_interaction ?? 0,
    token_efficiency_indicator: normalizeTokenEfficiencyIndicator(value?.token_efficiency_indicator),
    main_semantic_tags: Array.isArray(value?.main_semantic_tags) ? value.main_semantic_tags : [],
    per_task_token_totals: perTaskTokenTotals
  };
}

function toQuestionRequest(question: Question) {
  return {
    title: question.title,
    task_type: normalizeTaskType(question.task_type),
    difficulty: normalizeDifficulty(question.difficulty),
    verification_mode: normalizeVerificationMode(question.verification_mode, question.task_type),
    starter_prototype_reference: question.starter_prototype_reference ?? null,
    problem_description_markdown: question.problem_description_markdown,
    language_constraints: normalizeStudentLanguageConstraints(question.language_constraints),
    starter_code: {
      python: question.starter_code.python ?? {},
      javascript: question.starter_code.javascript ?? {},
      typescript: question.starter_code.typescript ?? {}
    },
    starter_files_metadata: question.starter_files_metadata ?? {},
    verification_metadata: question.verification_metadata ?? {},
    grading_configuration: question.grading_configuration ?? {},
    authoring_source: question.authoring_source ?? "manual",
    traceability_metadata: question.traceability_metadata ?? {},
    admin_notes: question.admin_notes ?? "",
    sort_order: question.sort_order ?? 0,
    max_score: question.max_score ?? 100
  };
}

function toTestCaseRequest(testCase: AdminTestCase) {
  return {
    name: testCase.name,
    visibility: testCase.visibility,
    test_code: normalizeTestCode(testCase.test_code),
    authoring_source: testCase.authoring_source ?? "manual",
    public_metadata: testCase.public_metadata ?? {},
    admin_metadata: testCase.admin_metadata ?? {},
    traceability_metadata: testCase.traceability_metadata ?? {}
  };
}

function normalizeAttemptStatus(value: string | undefined): AttemptStatus {
  if (value === "active" || value === "expired" || value === "submitted" || value === "closed") {
    return value;
  }

  return "not_started";
}

function normalizeStudentLanguageConstraints(value: unknown): Language[] {
  if (!Array.isArray(value)) {
    return ["python", "javascript"];
  }

  const languages = value.filter((item): item is Language => item === "python" || item === "javascript");
  return languages.length ? languages : ["python", "javascript"];
}

function normalizeTaskType(value: string | undefined): TaskType {
  if (value === "frontend_ui_extension" || value === "rest_api_development" || value === "database_query_schema" || value === "bug_fix") {
    return value;
  }

  if (value === "web_application") {
    return "frontend_ui_extension";
  }

  if (value === "api_development") {
    return "rest_api_development";
  }

  if (value === "database_task") {
    return "database_query_schema";
  }

  return "rest_api_development";
}

function normalizeDifficulty(value: string | undefined) {
  if (value === "easy" || value === "medium" || value === "hard") {
    return value;
  }

  return "medium";
}

function normalizeVerificationMode(value: string | undefined, taskType?: string): VerificationMode {
  if (value === "browser_ui_preview" || value === "api_response_check" || value === "database_result_check" || value === "automated_test" || value === "regression_test") {
    return value;
  }

  switch (normalizeTaskType(taskType)) {
    case "frontend_ui_extension":
      return "browser_ui_preview";
    case "rest_api_development":
      return "api_response_check";
    case "database_query_schema":
      return "database_result_check";
    case "bug_fix":
      return "regression_test";
    default:
      return "automated_test";
  }
}

function normalizeAuthoringSource(value: string | undefined) {
  if (value === "llm_generated" || value === "admin_edited") {
    return value;
  }

  return "manual";
}

function normalizeTokenEfficiencyIndicator(value: string | undefined): TokenEfficiencyIndicator {
  if (
    value === "no_ai_usage"
    || value === "strategic"
    || value === "token_heavy_success"
    || value === "inefficient"
    || value === "needs_review"
  ) {
    return value;
  }

  return "needs_review";
}

function normalizeMetadata(value: unknown): Record<string, string> {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>).map(([key, metadataValue]) => [
      key,
      typeof metadataValue === "string" ? metadataValue : String(metadataValue ?? "")
    ])
  );
}

function normalizeNestedMetadata(value: unknown): Record<string, Record<string, string>> {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>).map(([key, metadataValue]) => [
      key,
      normalizeMetadata(metadataValue)
    ])
  );
}
