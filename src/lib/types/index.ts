export type Role = "student" | "administrator";
export type Language = "python" | "javascript" | "typescript";
export type AssessmentStatus = "draft" | "active" | "closed" | "archived";
export type AttemptStatus = "not_started" | "active" | "expired" | "submitted" | "closed";
export type SubmissionStatus = "passed" | "failed" | "runtime_error" | "submitted";
export type AiHintLevel = "concept_hint" | "strategy_hint" | "debugging_hint" | "pseudocode_hint" | "code_level_suggestion";

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

export interface StarterCode {
  python: string;
  javascript: string;
  typescript: string;
}

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
}

export interface Question {
  question_id: string;
  title: string;
  problem_description_markdown: string;
  admin_notes?: string | null;
  sort_order?: number;
  max_score?: number;
  constraints: string[];
  language_constraints: Language[];
  starter_code: StarterCode;
  public_examples: PublicTestCase[];
  admin_test_cases?: AdminTestCase[];
  difficulty?: "easy" | "medium" | "hard";
  ai_credit_budget?: number;
}

export interface Assessment {
  assessment_id: string;
  title: string;
  description: string;
  duration_minutes: number;
  status: AssessmentStatus;
  ai_enabled: boolean;
  closes_at: string;
  question_count: number;
  attempt_status?: AttemptStatus;
  progress_percent?: number;
  score?: number;
  questions: Question[];
  submission_id?: string;
  ai_settings?: AssessmentAiSettings;
}

export interface AssessmentAiSettings {
  structured_hints_enabled: boolean;
  ai_credits_enabled: boolean;
  ai_rescue_enabled: boolean;
  reflection_enabled: boolean;
  rescue_correctness_probability: number;
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
  ai_credits_remaining?: number | null;
}

export interface WorkspaceState {
  assessment_id?: string;
  questions: Record<string, WorkspaceQuestionState>;
}

export interface AiState {
  assessment_id: string;
  ai_enabled: boolean;
  ai_settings: AssessmentAiSettings;
  hint_levels: Array<{ hint_level: AiHintLevel; credit_cost: number }>;
  rescue_chances_remaining: number;
  questions: Record<string, { ai_credit_budget: number; ai_credits_remaining: number | null }>;
}

export interface AiHintResponse {
  interaction_id: string;
  response_markdown: string;
  hint_level: AiHintLevel;
  credit_cost: number;
  credits_remaining: number | null;
  semantic_tags: string[];
  created_at: string;
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
      main_semantic_tags: string[];
    };
  }>;
}
