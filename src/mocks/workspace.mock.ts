import type { RunResult, SubmissionResult, WorkspaceState } from "@/lib/types";

export const mockWorkspace: WorkspaceState = {
  assessment_id: "algorithms-2026",
  questions: {
    "q-two-sum": {
      selected_language: "python",
      active_file: "main.py",
      files: {
        "main.py": {
          language: "python",
          content: "def solve(nums, target):\n    seen = {}\n    for index, value in enumerate(nums):\n        needed = target - value\n        if needed in seen:\n            return [seen[needed], index]\n        seen[value] = index\n"
        }
      },
      last_saved_at: "2026-05-02T12:22:00Z",
      version: 12
    }
  }
};

export const mockRunResult: RunResult = {
  execution_id: "mock-run-001",
  status: "passed",
  stdout: "sample 1 passed\nsample 2 passed\n",
  stderr: null,
  test_results: [
    { name: "sample 1", visibility: "public", passed: true, actual_output: "[0,1]", expected_output: "[0,1]" },
    { name: "sample 2", visibility: "public", passed: true, actual_output: "[1,2]", expected_output: "[1,2]" }
  ],
  metrics: { cpu_time_seconds: 0.04, peak_memory_kb: 11800 }
};

export const mockSubmissionResult: SubmissionResult = {
  submission_id: "mock-submission-001",
  evaluation_status: "passed",
  score: 92,
  max_score: 100,
  stdout: "Final submission accepted. Hidden case details are withheld.",
  stderr: null,
  submitted_at: "2026-05-02T12:35:00Z",
  visible_test_summary: { passed: 2, failed: 0, total: 2 },
  hidden_test_summary: { passed: 7, failed: 1, total: 8 }
};
