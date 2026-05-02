import type { StudentDashboard } from "@/lib/types";

export const mockStudentDashboard: StudentDashboard = {
  summary: {
    available_assessments: 2,
    in_progress_attempts: 1,
    completed_assessments: 1,
    average_score: 86
  },
  recent_activity: [
    { label: "Workspace autosaved", detail: "Pair Sum Indices version 12", timestamp: "2 min ago" },
    { label: "AI hint used", detail: "Asked for data structure direction", timestamp: "18 min ago" },
    { label: "Result published", detail: "Python Basics Retake scored 86%", timestamp: "Apr 10" }
  ]
};
