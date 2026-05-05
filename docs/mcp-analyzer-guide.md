# Using AI Models to Validate Project Requirements

This guide explains how to connect external AI assistants (like Claude, Cursor, or other MCP-compatible clients) to the custom `mcp-code-analyzer` tool. This allows any modern AI to automatically read your `SPEC.md` and check if the project codebase fulfills the requirements.

## 1. What is the MCP Code Analyzer?

The **Model Context Protocol (MCP)** is an open standard that allows AI models to securely interact with local tools. We have built a custom MCP server called `mcp-code-analyzer` that contains a specific tool:
- `scan_requirements_compliance`: Scans your `SPEC.md` and codebase heuristics to generate an audit report on what requirements are met and what is missing.

## 2. Prerequisites

The code analyzer requires Node.js. Make sure the analyzer is built before connecting it to an AI client:

```bash
cd mcp-code-analyzer
npm install
npm run build
```

The executable script will be located at `mcp-code-analyzer/build/index.js`.

## 3. Configuring Clients to use the Analyzer

### Method A: Claude Desktop

If you use the official Claude Desktop app, you can add the analyzer to its MCP configurations.

1. Open your Claude Desktop configuration file:
   - **Mac:** `~/Library/Application Support/Claude/claude_desktop_config.json`
   - **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
2. Add the following entry to the `"mcpServers"` object:

```json
{
  "mcpServers": {
    "project-analyzer": {
      "command": "node",
      "args": [
        "/ABSOLUTE/PATH/TO/YOUR/AISD-SS26-Group-7/mcp-code-analyzer/build/index.js"
      ]
    }
  }
}
```
*(Make sure to replace `/ABSOLUTE/PATH/TO/YOUR/...` with the actual path to the project directory on your machine.)*

3. Restart the Claude Desktop app. You should see a "hammer" or "plug" icon indicating the tool is loaded.

### Method B: Cursor (or other MCP IDEs)

1. Open Cursor Settings.
2. Navigate to **Features > MCP**.
3. Add a new MCP server:
   - **Name:** `mcp-code-analyzer`
   - **Type:** `stdio`
   - **Command:** `node /ABSOLUTE/PATH/TO/YOUR/AISD-SS26-Group-7/mcp-code-analyzer/build/index.js`
4. Enable the server. Cursor's AI (using Claude 3.5 Sonnet, GPT-4o, etc.) can now call this tool during chats.

## 4. Example Prompts to Ask the AI

Once connected, you can ask the AI model prompts like:

- *"Use the `scan_requirements_compliance` tool to check this project. The workspace root is `/ABSOLUTE/PATH/TO/YOUR/AISD-SS26-Group-7/`."*
- *"I just added a new endpoint for Inline Completions. Can you run the requirements scanner and verify what remaining AI capabilities are missing from the SPEC?"*
- *"Scan the project for compliance. Identify all requirements mapped to the 'Workspace' category that still need manual checks."*

The AI will execute the local JavaScript codebase, parse the `SPEC.md`, correlate the file system heuristics, and give you intelligent advice based on the output.
