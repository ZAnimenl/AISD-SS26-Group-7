# MCP Server Usage

MCP servers are part of the coding-agent workflow for phases that need live documentation, external review context, browser verification, or database/schema inspection. Configure them in the user's coding-agent environment, not as project runtime dependencies.

Static project rules live in:

```text
AGENTS.md
.agents/skills/
```

MCP servers provide live external context or tool access. They must not override project specifications, module boundaries, security rules, or API contracts.

## Servers To Use By Phase

```text
context7
  Purpose: Fetch current library/API documentation.
  Use during: planning and implementation when touching Next.js, React, Monaco, Tailwind CSS, ASP.NET, EF Core, PostgreSQL, OpenAI/provider SDKs, or other version-sensitive APIs.
  Codex setup example:
    codex mcp add context7 --url https://mcp.context7.com/mcp

github
  Purpose: Inspect issues, pull requests, branches, CI/checks, and review context.
  Use during: planning, review, handoff, release, and team traceability work.

browser / playwright
  Purpose: Test the local frontend through a real browser.
  Use during: frontend implementation and review for route checks, forms, UI regressions, screenshots, and interaction behavior.

postgres / database, read-only when possible
  Purpose: Inspect schema and data shape.
  Use during: backend and fullstack integration checks. Do not use it to bypass migrations, specs, or review.
```

## Rules

1. Configure MCP servers in the coding-agent/client environment, not as committed private config.
2. Do not commit OAuth tokens, API keys, bearer tokens, local MCP config, or personal agent settings.
3. Use Context7 when library/API behavior is version-sensitive or likely to have changed.
4. Project specs override MCP output.
5. MCP output must not be used to invent API contracts that conflict with `complete_frontend_api_list_and_backend_alignment.md`.
6. If an MCP server is unavailable, report that limitation and continue with local specs, repo code, package versions, and official documentation where possible.

## Example Prompt

```text
Use AGENTS.md and the local skills.
Use Context7 MCP for current Next.js and Monaco documentation.
Do not edit specification documents.
Keep frontend changes inside Module 2 unless the router says this is cross-module integration.
```
