import type { AggregateReport, ReportListItem } from "@/lib/types";

export const mockReportList: ReportListItem[] = [
  { assessment_id: "algorithms-2026", assessment_title: "Algorithms Foundations", average_score: 82.5, completion_count: 42, participant_count: 50, ai_interactions: 312 },
  { assessment_id: "python-basics", assessment_title: "Python Basics Retake", average_score: 76.2, completion_count: 31, participant_count: 34, ai_interactions: 0 },
  { assessment_id: "js-data-structures", assessment_title: "JavaScript Data Structures", average_score: 69.8, completion_count: 18, participant_count: 29, ai_interactions: 190 }
];

export const mockAggregateReports: Record<string, AggregateReport> = {
  "algorithms-2026": {
    ...mockReportList[0],
    score_distribution: [
      { range: "0-20", count: 1 },
      { range: "21-40", count: 2 },
      { range: "41-60", count: 6 },
      { range: "61-80", count: 13 },
      { range: "81-100", count: 20 }
    ],
    students: [
      {
        user_id: "student-1",
        student_name: "Mira Student",
        student_email: "mira.student@example.edu",
        attempt_status: "submitted",
        submission_status: "passed",
        score: 92,
        max_score: 100,
        submitted_at: "2026-05-02T12:35:00Z",
        ai_usage_summary: { total_interactions: 8, main_semantic_tags: ["conceptual_hint", "debug"] }
      },
      {
        user_id: "student-2",
        student_name: "Jonas Keller",
        student_email: "jonas.keller@example.edu",
        attempt_status: "submitted",
        submission_status: "failed",
        score: 64,
        max_score: 100,
        submitted_at: "2026-05-02T11:50:00Z",
        ai_usage_summary: { total_interactions: 3, main_semantic_tags: ["explain"] }
      }
    ]
  }
};
