import type { Language } from "@/lib/types";

export const defaultTestCode: Record<Language, string> = {
  python: "from solution import solve\n\n\ndef test_solution_exists():\n    assert callable(solve)\n",
  javascript: "const { solve } = require(\"./solution.js\");\n\ntest(\"solution exists\", () => {\n  expect(typeof solve).toBe(\"function\");\n});\n",
  typescript: "const solve = globalThis.__ojsharpSolve;\n\ntest(\"solution exists\", () => {\n  expect(typeof solve).toBe(\"function\");\n});\n"
};

export function normalizeTestCode(testCode?: Partial<Record<Language, string>> | null): Record<Language, string> {
  return {
    python: testCode?.python ?? defaultTestCode.python,
    javascript: testCode?.javascript ?? defaultTestCode.javascript,
    typescript: testCode?.typescript ?? defaultTestCode.typescript
  };
}
