export type Role = "student" | "administrator";
export type Language = "python" | "javascript" | "typescript" | "html" | "sql";
export type AssessmentStatus = "draft" | "active" | "closed" | "archived";
export type AttemptStatus = "not_started" | "active" | "expired" | "submitted" | "closed";
export type ExecutionStatus = "queued" | "running" | "passed" | "failed" | "runtime_error" | "time_limit_exceeded" | "memory_limit_exceeded" | "internal_error";
export type SubmissionStatus = ExecutionStatus | "submitted" | "not_submitted";
export type AiInteractionType = "code_suggestion" | "explanation" | "debugging";
export type TaskType = "frontend_ui_extension" | "rest_api_development" | "database_query_schema" | "bug_fix";
export type Difficulty = "easy" | "medium" | "hard";
export type VerificationMode = "browser_ui_preview" | "api_response_check" | "database_result_check" | "automated_test" | "regression_test";
export type AuthoringSource = "manual" | "llm_generated" | "admin_edited";
export type AiGradingStatus = "not_required" | "reflection_pending" | "pending" | "completed" | "failed";
export type Metadata = Record<string, string>;

export interface AuthUser {
  user_id: string;
  name?: string;
  full_name?: string;
  username?: string;
  email: string;
  role: Role;
  status?: "active" | "inactive";
  created_at?: string;
}

export type UserAccount = Required<Pick<AuthUser, "user_id" | "full_name" | "username" | "email" | "role" | "status" | "created_at">>;

export type StarterCode = Record<Language, Record<string, string>>;

export interface PublicTestCase {
  test_case_id: string;
  name: string;
  visibility: "public";
}

export interface AdminTestCase {
  test_case_id: string;
  name: string;
  visibility: "public" | "hidden";
  test_code: Record<Language, string>;
  authoring_source?: AuthoringSource;
  public_metadata?: Metadata;
  admin_metadata?: Metadata;
  traceability_metadata?: Metadata;
}

export interface Question {
  question_id: string;
  title: string;
  task_type?: TaskType;
  difficulty?: Difficulty;
  verification_mode?: VerificationMode;
  starter_prototype_reference?: string | null;
  problem_description_markdown: string;
  admin_notes?: string | null;
  sort_order?: number;
  max_score?: number;
  constraints: string[];
  language_constraints: Language[];
  starter_code: StarterCode;
  starter_files_metadata?: Record<string, Metadata>;
  verification_metadata?: Metadata;
  grading_configuration?: Metadata;
  authoring_source?: AuthoringSource;
  traceability_metadata?: Metadata;
  public_examples: PublicTestCase[];
  admin_test_cases?: AdminTestCase[];
}

export interface Assessment {
  assessment_id: string;
  title: string;
  description: string;
  duration_minutes: number;
  starts_at?: string | null;
  expires_at?: string | null;
  status: AssessmentStatus;
  ai_enabled: boolean;
  shared_prototype_reference?: string | null;
  shared_prototype_version?: string | null;
  shared_prototype_metadata?: Metadata;
  supported_task_categories?: TaskType[];
  supported_verification_modes?: VerificationMode[];
  closes_at: string;
  question_count: number;
  attempt_status?: AttemptStatus;
  progress_percent?: number;
  score?: number;
  functional_score?: number;
  ai_usage_score?: number | null;
  final_score?: number | null;
  ai_grading_status?: AiGradingStatus;
  ai_grading_summary?: string | null;
  ai_grading_confidence?: string | null;
  ai_grading_details?: Record<string, unknown>;
  reflection_text?: string;
  reflection_submitted_at?: string | null;
  questions: Question[];
  submission_id?: string;
}

export interface WorkspaceFile {
  language: Language;
  content: string;
}

export interface WorkspaceQuestionState {
  selected_language: Language;
  active_file: string;
  files: Record<string, WorkspaceFile>;
  last_saved_at: string;
  version: number;
}

export interface WorkspaceState {
  assessment_id?: string;
  questions: Record<string, WorkspaceQuestionState>;
}

export interface RunResult {
  execution_id: string;
  status: ExecutionStatus;
  stdout: string;
  stderr: string | null;
  preview_document?: string | null;
  test_results: Array<{
    name: string;
    visibility: "public";
    passed: boolean;
    output: string;
  }>;
  metrics: {
    cpu_time_seconds: number;
    peak_memory_kb: number;
  };
}

export interface AiCodeSuggestion {
  target_file: string;
  language: Language;
  replacement_code: string;
  apply_label: string;
}

export type AiWorkspaceActionType = "replace_file" | "run_public_checks";

export interface AiWorkspaceAction {
  type: AiWorkspaceActionType;
  label: string;
  target_file?: string | null;
  language?: Language | null;
  replacement_code?: string | null;
}

export interface AiAssistantResponse {
  interaction_id: string;
  response_markdown: string;
  semantic_tags: string[];
  suggestion?: AiCodeSuggestion | null;
  workspace_actions?: AiWorkspaceAction[];
  token_usage: {
    input_tokens: number;
    output_tokens: number;
    total_tokens: number;
  };
}

export interface AiTaskTranscriptEntry {
  interaction_id: string;
  interaction_type: AiInteractionType;
  input: string;
  output: string;
  token_usage: {
    input_tokens: number;
    output_tokens: number;
    total_tokens: number;
  };
  created_at: string;
}

export interface AiTaskTranscript {
  assessment_id: string;
  question_id: string;
  interactions: AiTaskTranscriptEntry[];
}

export interface SubmissionResult {
  submission_id: string;
  evaluation_status: SubmissionStatus;
  score: number;
  max_score: number;
  functional_score: number;
  functional_max_score: number;
  ai_enabled: boolean;
  submission_state: AiGradingStatus | "completed";
  reflection_required: boolean;
  reflection_deadline: string | null;
  stdout: string;
  stderr: string | null;
  submitted_at: string;
  visible_test_summary: { passed: number; failed: number; total: number };
  hidden_test_summary: { passed: number; failed: number; total: number };
}

export interface SystemConfig {
  features: {
    registration_enabled: boolean;
    embedded_ai_agent_enabled: boolean;
    ai_chat_enabled: boolean;
    ai_inline_completion_enabled: boolean;
    token_tracking_enabled: boolean;
    multi_file_workspace_enabled: boolean;
    real_sandbox_enabled: boolean;
  };
  supported_languages: Language[];
  auth_method: string;
  roles: Role[];
}

export interface StudentDashboard {
  summary: {
    available_assessments: number;
    in_progress_attempts: number;
    completed_assessments: number;
    average_score: number;
  };
  recent_activity: Array<{ label: string; detail: string; timestamp: string }>;
}

export interface AdminDashboard {
  summary: {
    total_assessments: number;
    active_assessments: number;
    total_students: number;
    total_submissions: number;
    average_score: number;
    ai_interactions: number;
  };
  recent_submissions: Array<{ student_name: string; assessment_title: string; score: number; submitted_at: string }>;
}

export interface ReportListItem {
  assessment_id: string;
  assessment_title: string;
  average_score: number;
  ai_enabled: boolean;
  average_functional_score: number;
  average_ai_usage_score: number;
  average_final_score: number;
  completion_count: number;
  participant_count: number;
  ai_interactions: number;
  total_ai_tokens: number;
  average_ai_tokens_per_interaction: number;
}

export interface AiTaskTokenTotal {
  question_id: string;
  task_title: string;
  task_type: string;
  interaction_count: number;
  total_input_tokens: number;
  total_output_tokens: number;
  total_tokens: number;
}

export interface AiUsageSummary {
  total_interactions: number;
  total_tokens: number;
  total_input_tokens: number;
  total_output_tokens: number;
  average_tokens_per_interaction: number;
  main_semantic_tags: string[];
  per_task_token_totals: AiTaskTokenTotal[];
}

export interface AggregateReport extends ReportListItem {
  ai_usage_summary: AiUsageSummary;
  score_distribution: Array<{ range: string; count: number }>;
  students: Array<{
    attempt_id: string;
    user_id: string;
    student_name: string;
    student_email: string;
    attempt_status: AttemptStatus;
    submission_status: SubmissionStatus;
    score: number;
    max_score: number;
    functional_score: number;
    ai_usage_score: number | null;
    final_score: number | null;
    submitted_at: string;
    reflection: {
      text: string;
      word_count: number;
      submitted_at: string | null;
      submitted_by: string | null;
    };
    ai_grading: {
      status: AiGradingStatus;
      score: number | null;
      rubric_version: string | null;
      model: string | null;
      summary: string | null;
      confidence: string | null;
      graded_at: string | null;
      details: Record<string, unknown>;
    };
    ai_usage_summary: AiUsageSummary;
  }>;
}

export interface ReflectionState {
  assessment_id: string;
  reflection_text: string;
  word_count: number;
  reflection_deadline: string;
  reflection_submitted_at: string | null;
  reflection_submission_reason: string | null;
  grading_status: AiGradingStatus;
  ai_usage_score: number | null;
  grading_summary: string | null;
}
