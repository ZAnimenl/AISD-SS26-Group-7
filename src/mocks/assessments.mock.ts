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
        language_constraints: ["python", "javascript", "typescript"],
        starter_code: {
          python: "def solve(nums, target):\n    # Return two indices\n    pass\n",
          javascript: "function solve(nums, target) {\n  // Return two indices\n}\n",
          typescript: "function solve(nums: number[], target: number): number[] {\n  return [];\n}\n"
        },
        public_examples: [
          { test_case_id: "tc-public-1", name: "sample 1", visibility: "public" },
          { test_case_id: "tc-public-2", name: "sample 2", visibility: "public" }
        ],
        admin_test_cases: [
          {
            test_case_id: "tc-public-1",
            name: "sample 1",
            visibility: "public",
            test_code: {
              python: "from solution import solve\n\n\ndef test_sample():\n    assert solve([2, 7, 11, 15], 9) == [0, 1]\n",
              javascript: "const { solve } = require(\"./solution.js\");\n\ntest(\"sample\", () => {\n  expect(solve([2, 7, 11, 15], 9)).toEqual([0, 1]);\n});\n",
              typescript: "const solve = globalThis.__ojsharpSolve;\n\ntest(\"sample\", () => {\n  expect(solve([2, 7, 11, 15], 9)).toEqual([0, 1]);\n});\n"
            }
          },
          {
            test_case_id: "tc-hidden-1",
            name: "large duplicate set",
            visibility: "hidden",
            test_code: {
              python: "from solution import solve\n\n\ndef test_duplicate_values():\n    assert solve([3, 3], 6) == [0, 1]\n",
              javascript: "const { solve } = require(\"./solution.js\");\n\ntest(\"duplicate values\", () => {\n  expect(solve([3, 3], 6)).toEqual([0, 1]);\n});\n",
              typescript: "const solve = globalThis.__ojsharpSolve;\n\ntest(\"duplicate values\", () => {\n  expect(solve([3, 3], 6)).toEqual([0, 1]);\n});\n"
            }
          }
        ]
      },
      {
        question_id: "q-window",
        title: "Longest Stable Window",
        problem_description_markdown:
          "Find the longest contiguous segment where the difference between the minimum and maximum values is at most k.",
        constraints: ["Input length up to 20,000.", "Return the segment length.", "Public examples cover small arrays only."],
        language_constraints: ["python", "javascript", "typescript"],
        starter_code: {
          python: "def solve(values, k):\n    return 0\n",
          javascript: "function solve(values, k) {\n  return 0;\n}\n",
          typescript: "function solve(values: number[], k: number): number {\n  return 0;\n}\n"
        },
        public_examples: [
          { test_case_id: "tc-public-3", name: "sample 1", visibility: "public" }
        ],
        admin_test_cases: [
          {
            test_case_id: "tc-public-3",
            name: "sample 1",
            visibility: "public",
            test_code: {
              python: "from solution import solve\n\n\ndef test_sample_window():\n    assert solve([8, 2, 4, 7], 4) == 2\n",
              javascript: "const { solve } = require(\"./solution.js\");\n\ntest(\"sample window\", () => {\n  expect(solve([8, 2, 4, 7], 4)).toBe(2);\n});\n",
              typescript: "const solve = globalThis.__ojsharpSolve;\n\ntest(\"sample window\", () => {\n  expect(solve([8, 2, 4, 7], 4)).toBe(2);\n});\n"
            }
          },
          {
            test_case_id: "tc-hidden-2",
            name: "monotonic stress",
            visibility: "hidden",
            test_code: {
              python: "from solution import solve\n\n\ndef test_small_monotonic():\n    assert solve([1, 2, 3], 1) == 2\n",
              javascript: "const { solve } = require(\"./solution.js\");\n\ntest(\"small monotonic\", () => {\n  expect(solve([1, 2, 3], 1)).toBe(2);\n});\n",
              typescript: "const solve = globalThis.__ojsharpSolve;\n\ntest(\"small monotonic\", () => {\n  expect(solve([1, 2, 3], 1)).toBe(2);\n});\n"
            }
          }
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
