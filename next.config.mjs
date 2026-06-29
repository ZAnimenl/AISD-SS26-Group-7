import { fileURLToPath } from "node:url";

const ignoredDevWatchPaths = /(?:^|[\\/])(?:\.agents|\.git|\.idea|\.local-data|\.next|\.pytest_cache|\.sixth|\.vercel|Backend|assessmentPrototype|deploy|docs|mcp-code-analyzer|node_modules|output|outputs|tmp)(?:[\\/]|$)|(?:^|[\\/])(?:backend-dev|backend-only-session|dev-session|frontend-session).*\.log$/;

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  turbopack: {
    root: fileURLToPath(new URL(".", import.meta.url))
  },
  webpack(config, { dev }) {
    if (dev) {
      config.watchOptions = {
        ...config.watchOptions,
        ignored: ignoredDevWatchPaths
      };
    }

    return config;
  }
};

export default nextConfig;
