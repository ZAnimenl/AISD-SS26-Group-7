# AI Provider Enforcement

## Problem Definition

The active engineering contract forbids delivering mock behavior as a real
runtime feature. The AI assistance path must therefore use a configured real AI
provider or fail explicitly. It must not fabricate mock guidance when no
provider is available.

## Option Comparison

### Option A: Keep mock fallback responses

- Pros: local runs always return assistant text.
- Cons: deployed behavior can appear functional without a real AI provider,
  which violates the no-mock-delivery rule.

### Option B: Keep provider fallback only in production

- Pros: preserves local convenience.
- Cons: creates environment-dependent behavior and weakens verification.

### Option C: Use provider-backed completion and fail closed

- Pros: runtime behavior is truthful, token telemetry comes from provider usage,
  and missing dependencies are visible.
- Cons: local AI assistance requires configuring DeepSeek or a local OpenAI-
  compatible provider.

Chosen option: Option C.

## Research Basis

- DeepSeek official Chat Completions documentation defines `POST
  /chat/completions` and response token usage fields:
  `https://api-docs.deepseek.com/api/create-chat-completion`.
- DeepSeek official JSON Output guidance uses `response_format` with
  `{"type":"json_object"}` for JSON responses:
  `https://api-docs.deepseek.com/guides/json_mode/`.

## State Machine

States:

- `request-received`: student AI request passed authentication and assessment
  guards.
- `safety-blocked`: request asks for a direct complete solution.
- `provider-selected`: at least one provider is enabled and configured.
- `provider-completed`: provider returned content and token usage.
- `provider-unavailable`: no provider returned usable content and usage.
- `interaction-recorded`: response and token usage are persisted.
- `error-returned`: API returns a structured error without recording fake AI
  usage.

Events and transitions:

- `direct-solution-detected`: `request-received` to `safety-blocked`.
- `provider-configured`: `request-received` to `provider-selected`.
- `provider-success`: `provider-selected` to `provider-completed`.
- `provider-failure`: `provider-selected` to `provider-unavailable`.
- `record-response`: `safety-blocked` or `provider-completed` to
  `interaction-recorded`.
- `no-provider`: `provider-unavailable` to `error-returned`.

Guards:

- Student role is required.
- The assessment must be active and AI-enabled.
- The active attempt is resolved by authenticated user plus `assessment_id`.
- Hidden tests, grading criteria, and provider secrets are not sent to the
  frontend.

Side effects:

- Provider-backed or safety-blocked responses are recorded as AI interactions.
- Missing provider configuration returns `AI_PROVIDER_UNAVAILABLE`.

Failure paths:

- Provider errors are logged server-side and result in a structured 503 API
  error when no provider succeeds.
- Direct complete-solution requests return a safety response without calling the
  provider.

Rollback path:

- Restore the previous AI service registration and endpoint dependency.
- Restore deleted provider/mock files from git history if the owner explicitly
  accepts mock fallback behavior.

## Impact Surface

- Module 4 backend AI assistance service and dependency injection.
- Student AI assistance API error behavior when no real provider is available.
- AI interaction persistence for provider-backed and safety-blocked responses.

No frontend API contract shape changes are required beyond existing error
handling.

## Primitive Acceptance Criteria

- Backend build succeeds without mock AI service references.
- Backend tests pass.
- AI assistance uses `AiAssistantService` and `AiCompletionService`.
- No `AiMockService`, `IAiResponseProvider`, `DeepseekAiResponseProvider`, or
`LocalLlmAiResponseProvider` registration remains.
- AI-generated assessment and question drafts use provider-backed completion
  and do not fall back to template-generated content labeled as LLM output.
- When no provider returns usable content and usage, the API returns
  `AI_PROVIDER_UNAVAILABLE` with HTTP 503.
