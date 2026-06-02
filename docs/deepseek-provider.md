# DeepSeek Provider Setup

The backend AI assistance service can call any OpenAI-compatible chat-completions provider through the `LocalLlm` configuration section. Despite the section name, it is not limited to local models.

DeepSeek is supported through this configuration because DeepSeek exposes an OpenAI-compatible API.

## Environment Variables

Set these values before starting the backend:

```powershell
$env:LocalLlm__Enabled = "true"
$env:LocalLlm__BaseUrl = "https://api.deepseek.com"
$env:LocalLlm__Model = "deepseek-chat"
$env:LocalLlm__ApiKey = "<your DeepSeek API key>"
```

For the reasoning model, use:

```powershell
$env:LocalLlm__Model = "deepseek-reasoner"
```

DeepSeek's own examples often call the key `DEEPSEEK_API_KEY`. The current ASP.NET backend does not read that alias directly; it reads `LocalLlm__ApiKey`.

## Runtime Behavior

When `LocalLlm__Enabled=false`, or when the configured provider is unavailable, the backend falls back to logged mock guidance. This keeps the assessment demo usable without exposing provider keys or requiring a paid hosted provider.

The frontend never calls DeepSeek directly. All AI requests go through backend endpoints so that the backend can enforce authentication, assessment context, structured AI modes, AI credit rules, Rescue safeguards, provider-secret protection, and interaction logging.

## Backend AI Endpoints

The existing connected implementation uses:

```text
POST /api/v1/assessments/{assessment_id}/questions/{question_id}/ai/chat
```

The updated contract in `SPEC.md` moves assessment-time help toward structured endpoints such as:

```text
POST /api/v1/assessments/{assessment_id}/questions/{question_id}/ai/hints
POST /api/v1/assessments/{assessment_id}/questions/{question_id}/ai/rescue
```

The same provider configuration can support structured hints, AI Rescue, task generation, and reflection evaluation once those features are implemented.
