import type { AiInteractionType } from "@/lib/types";

export const mockAiResponses: Record<AiInteractionType, string> = {
  chat: "I can help reason about the approach without solving the whole assessment for you.",
  hint: "Think about storing values you have already seen so each new value can ask what complement it needs.",
  explain: "The map-based approach trades a small amount of memory for a single pass through the input.",
  debug: "Check whether you store the current value before or after checking for its complement; the order matters for duplicate values.",
  code_review: "The structure is concise. Consider returning indices in ascending order and adding a guard for malformed input if the platform contract allows it."
};
