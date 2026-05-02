import type { AdminDashboard } from "@/lib/types";

export const mockAdminDashboard: AdminDashboard = {
  summary: {
    total_assessments: 8,
    active_assessments: 3,
    total_students: 124,
    total_submissions: 318,
    average_score: 78.4,
    ai_interactions: 940
  },
  recent_submissions: [
    { student_name: "Mira Student", assessment_title: "Algorithms Foundations", score: 92, submitted_at: "12 min ago" },
    { student_name: "Jonas Keller", assessment_title: "Python Basics Retake", score: 81, submitted_at: "1 hour ago" },
    { student_name: "Lea Weber", assessment_title: "JavaScript Data Structures", score: 74, submitted_at: "Yesterday" }
  ]
};
