export type Role = "student" | "administrator";
export type Language = "python" | "javascript";
export type AssessmentStatus = "draft" | "active" | "closed" | "archived";
export type AttemptStatus = "not_started" | "active" | "expired" | "submitted" | "closed";
export type SubmissionStatus = "passed" | "failed" | "runtime_error" | "submitted";
export type AiInteractionType = "chat" | "hint" | "explain" | "debug" | "code_review";

export interface MockUser {
  user_id: string;
  name: string;
  email: string;
  role: Role;
}

export interface StarterCode {
  python: string;
  javascript: string;
}

export interface PublicTestCase {
  test_case_id: string;
  name: string;
  visibility: "public";
  input: string;
  expected_output: string;
}

export interface AdminTestCase {
  test_case_id: string;
  name: string;
  visibility: "public" | "hidden";
  input_preview: string;
  expected_output_preview: string;
  points: number;
}

export interface Question {
  question_id: string;
  title: string;
  problem_description_markdown: string;
  constraints: string[];
  language_constraints: Language[];
  starter_code: StarterCode;
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
  closes_at: string;
  question_count: number;
  attempt_status?: AttemptStatus;
  progress_percent?: number;
  score?: number;
  questions: Question[];
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
  assessment_id: string;
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
    actual_output: string;
    expected_output: string;
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
