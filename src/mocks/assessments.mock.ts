import type { Assessment } from "@/lib/types";

export const mockAssessments: Assessment[] = [
  {
    assessment_id: "algorithms-2026",
    title: "Algorithms Foundations",
    description: "Solve array, string, and complexity-focused coding tasks with AI assistance enabled.",
    duration_minutes: 90,
    status: "active",
    ai_enabled: true,
    closes_at: "2026-06-07T18:00:00Z",
    question_count: 2,
    attempt_status: "active",
    progress_percent: 48,
    questions: [
      {
        question_id: "q-two-sum",
        title: "Pair Sum Indices",
        problem_description_markdown:
          "Given a list of integers and a target value, return the indices of two distinct elements whose values add up to the target.",
        constraints: ["Exactly one valid pair exists.", "Return indices in ascending order.", "Aim for better than O(n^2)."],
        language_constraints: ["python", "javascript"],
        starter_code: {
          python: "def solve(nums, target):\n    # Return two indices\n    pass\n",
          javascript: "function solve(nums, target) {\n  // Return two indices\n}\n"
        },
        public_examples: [
          { test_case_id: "tc-public-1", name: "sample 1", visibility: "public", input: "nums=[2,7,11,15], target=9", expected_output: "[0,1]" },
          { test_case_id: "tc-public-2", name: "sample 2", visibility: "public", input: "nums=[3,2,4], target=6", expected_output: "[1,2]" }
        ],
        admin_test_cases: [
          { test_case_id: "tc-public-1", name: "sample 1", visibility: "public", input_preview: "[2,7,11,15], 9", expected_output_preview: "[0,1]", points: 10 },
          { test_case_id: "tc-hidden-1", name: "large duplicate set", visibility: "hidden", input_preview: "10k values, target near tail", expected_output_preview: "metadata only", points: 30 }
        ]
      },
      {
        question_id: "q-window",
        title: "Longest Stable Window",
        problem_description_markdown:
          "Find the longest contiguous segment where the difference between the minimum and maximum values is at most k.",
        constraints: ["Input length up to 20,000.", "Return the segment length.", "Public examples cover small arrays only."],
        language_constraints: ["python", "javascript"],
        starter_code: {
          python: "def solve(values, k):\n    return 0\n",
          javascript: "function solve(values, k) {\n  return 0;\n}\n"
        },
        public_examples: [
          { test_case_id: "tc-public-3", name: "sample 1", visibility: "public", input: "values=[8,2,4,7], k=4", expected_output: "2" }
        ],
        admin_test_cases: [
          { test_case_id: "tc-public-3", name: "sample 1", visibility: "public", input_preview: "[8,2,4,7], 4", expected_output_preview: "2", points: 10 },
          { test_case_id: "tc-hidden-2", name: "monotonic stress", visibility: "hidden", input_preview: "20k ordered values", expected_output_preview: "metadata only", points: 40 }
        ]
      }
    ]
  },
  {
    assessment_id: "js-data-structures",
    title: "JavaScript Data Structures",
    description: "Practice maps, stacks, and clean function design in JavaScript or Python.",
    duration_minutes: 60,
    status: "active",
    ai_enabled: true,
    closes_at: "2026-06-14T18:00:00Z",
    question_count: 3,
    attempt_status: "not_started",
    progress_percent: 0,
    questions: []
  },
  {
    assessment_id: "python-basics",
    title: "Python Basics Retake",
    description: "A closed assessment shown in results and reports.",
    duration_minutes: 45,
    status: "closed",
    ai_enabled: false,
    closes_at: "2026-04-10T18:00:00Z",
    question_count: 2,
    attempt_status: "submitted",
    progress_percent: 100,
    score: 86,
    questions: []
  }
];
