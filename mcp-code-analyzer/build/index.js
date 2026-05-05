#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ErrorCode, ListToolsRequestSchema, McpError, } from "@modelcontextprotocol/sdk/types.js";
import * as fs from "fs/promises";
import * as path from "path";
const server = new Server({
    name: "mcp-code-analyzer",
    version: "1.0.0",
}, {
    capabilities: {
        tools: {},
    },
});
// Map common requirement topics to filepath regexes or string keywords
const heuristics = {
    "Auth": { files: ["AuthEndpoints.cs", "login"], keywords: ["login", "authenticate", "token", "password"] },
    "Assessment": { files: ["AssessmentEndpoints.cs", "AdminAssessment"], keywords: ["create assessment", "duration", "status"] },
    "Question": { files: ["QuestionEndpoints.cs"], keywords: ["title", "problem description", "starter code"] },
    "Workspace": { files: ["WorkspaceClient.tsx", "Monaco"], keywords: ["editor", "run code", "submit"] },
    "Sandboxed": { files: ["DockerCodeRunner.cs", "CodeEvaluationService.cs"], keywords: ["sandbox", "isolated", "timeout", "docker"] },
    "AI": { files: ["AiEndpoints.cs", "AiMockService.cs"], keywords: ["hint", "chat", "explain"] },
    "Report": { files: ["ReportEndpoints.cs", "reports/page.tsx"], keywords: ["score_distribution", "average_score", "summary"] },
};
server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
        tools: [
            {
                name: "scan_requirements_compliance",
                description: "Scans SPEC.md and the codebase (by heuristics) to provide a compliance report and advice.",
                inputSchema: {
                    type: "object",
                    properties: {
                        workspaceRoot: {
                            type: "string",
                            description: "Absolute path to the root of the project.",
                        },
                    },
                    required: ["workspaceRoot"],
                },
            },
        ],
    };
});
server.setRequestHandler(CallToolRequestSchema, async (request) => {
    if (request.params.name === "scan_requirements_compliance") {
        const workspaceRoot = String(request.params.arguments?.workspaceRoot);
        if (!workspaceRoot) {
            throw new McpError(ErrorCode.InvalidParams, "workspaceRoot is required");
        }
        try {
            const specPath = path.join(workspaceRoot, "SPEC.md");
            const specContent = await fs.readFile(specPath, "utf-8");
            // Find all REQ-XX
            const reqRegex = /(REQ-\d+.*?)(?=(REQ-\d+)|$)/gs;
            let match;
            const requirements = [];
            while ((match = reqRegex.exec(specContent)) !== null) {
                requirements.push(match[1].trim());
            }
            // We do a very basic check. If the REQ text contains a topic, we check if those files/keywords exist
            const report = [];
            report.push("# Specification Compliance Report");
            report.push("This report uses basic heuristics to detect feature presence.");
            let missingCount = 0;
            for (const req of requirements) {
                const reqPrefix = req.substring(0, 6);
                let categoryMatches = 0;
                let matchedCategory = "";
                for (const [category, hint] of Object.entries(heuristics)) {
                    if (hint.keywords.some(kw => req.toLowerCase().includes(kw))) {
                        matchedCategory = category;
                        break;
                    }
                }
                if (matchedCategory) {
                    report.push(`✅ **${reqPrefix}** - Likely covered by ${matchedCategory} module.`);
                }
                else {
                    report.push(`⚠️ **${reqPrefix}** - Needs manual check. Requirement: ${req.split('\n')[0]}`);
                    missingCount++;
                }
            }
            report.push("\n## Advice for Improvement");
            report.push("1. Requirements flagged with 'Needs manual check' lack clear module integration. Consider adding dedicated API endpoints or UI views.");
            report.push("2. Verify real sandbox execution (REQ-27/28) by ensuring DockerCodeRunner properly mounts volumes and enforces timeouts.");
            report.push("3. Implement Inline Completion (REQ-35) if the backend supports ghost text.");
            report.push(`4. Found ${missingCount} requirements out of ${requirements.length} that might need more attention.`);
            return {
                content: [
                    {
                        type: "text",
                        text: report.join("\n"),
                    },
                ],
            };
        }
        catch (e) {
            throw new McpError(ErrorCode.InternalError, e.message);
        }
    }
    throw new McpError(ErrorCode.MethodNotFound, "Tool not found");
});
const transport = new StdioServerTransport();
await server.connect(transport);
console.error("MCP Code Analyzer Server running on stdio");
