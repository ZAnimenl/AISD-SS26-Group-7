export type Role = "student" | "administrator";
export type Language = "python" | "javascript" | "typescript";
export type AssessmentStatus = "draft" | "active" | "closed" | "archived";
export type AttemptStatus = "not_started" | "active" | "expired" | "submitted" | "closed";
export type SubmissionStatus = "passed" | "failed" | "runtime_error" | "submitted";
export type AiInteractionType = "code_suggestion" | "explanation" | "debugging";
export type TaskType = "frontend_ui_extension" | "rest_api_development" | "database_query_schema" | "bug_fix";
export type Difficulty = "easy" | "medium" | "hard";
export type VerificationMode = "browser_ui_preview" | "api_response_check" | "database_result_check" | "automated_test" | "regression_test";
export type AuthoringSource = "manual" | "llm_generated" | "admin_edited";
export type Metadata = Record<string, string>;

export interface AuthUser {
  user_id: string;
  name?: string;
  full_name?: string;
  email: string;
  role: Role;
  status?: "active" | "inactive";
  created_at?: string;
}

export type UserAccount = Required<Pick<AuthUser, "user_id" | "full_name" | "email" | "role" | "status" | "created_at">>;

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
  status: "passed" | "failed" | "runtime_error";
  stdout: string;
  stderr: string | null;
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

export interface SubmissionResult {
  submission_id: string;
  evaluation_status: SubmissionStatus;
  score: number;
  max_score: number;
  stdout: string;
  stderr: string | null;
  submitted_at: string;
  visible_test_summary: { passed: number; failed: number; total: number };
  hidden_test_summary: { passed: number; failed: number; total: number };
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
  completion_count: number;
  participant_count: number;
  ai_interactions: number;
}

export interface AggregateReport extends ReportListItem {
  score_distribution: Array<{ range: string; count: number }>;
  students: Array<{
    user_id: string;
    student_name: string;
    student_email: string;
    attempt_status: AttemptStatus;
    submission_status: SubmissionStatus;
    score: number;
    max_score: number;
    submitted_at: string;
    ai_usage_summary: {
      total_interactions: number;
      total_tokens: number;
      total_input_tokens: number;
      total_output_tokens: number;
      average_tokens_per_interaction: number;
      main_semantic_tags: string[];
    };
  }>;
}
