import nextCoreWebVitals from "eslint-config-next/core-web-vitals";

const eslintConfig = [
  ...nextCoreWebVitals,
  {
    ignores: [
      ".next/**",
      "Backend/**/bin/**",
      "Backend/**/obj/**",
      "mcp-code-analyzer/build/**",
      "mcp-code-analyzer/node_modules/**",
      "node_modules/**",
      "tsconfig.tsbuildinfo"
    ]
  }
];

export default eslintConfig;
