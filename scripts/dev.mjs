#!/usr/bin/env node

import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import fs from "node:fs";
import fsp from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import readline from "node:readline/promises";
import { Writable } from "node:stream";
import { fileURLToPath, pathToFileURL } from "node:url";
import {
  derivePostgresConnectionFromEnvironment,
  diagnoseBackendFailure,
  isUsableConfigValue,
  normalizePostgresConnectionString,
  parseDotnetUserSecrets
} from "./dev-support.mjs";
import {
  cleanLocalAiConfig,
  isAcceptableDeepseekApiKey,
  normalizeDeepseekApiKey,
  normalizeEffectiveAiConfig
} from "./dev-ai-config.mjs";
import {
  parseEnvFileContent,
  serializeEnvFile as serializeEnvFileContent
} from "./dev-env-file.mjs";
import {
  buildLocalDatabaseConfig,
  ensureLocalDatabaseConfig,
  isSqliteConnectionString,
  localDatabase,
  localSeedAdminDefaults
} from "./dev-local-database.mjs";
import {
  createCommandRunner
} from "./dev-command-runner.mjs";
import {
  findListeningProcessIds,
  findProcessIdsByCommand,
  isSafeBackendProcessCommand,
  isSafeFrontendProcessCommand,
  readProcessCommand,
  resolveUrlPort,
  stopProcessIds
} from "./dev-port-processes.mjs";

export {
  convertPostgresUrlToNpgsql,
  diagnoseBackendFailure,
  isUsableConfigValue,
  parseDotnetUserSecrets
} from "./dev-support.mjs";
export {
  cleanLocalAiConfig,
  isAcceptableDeepseekApiKey,
  normalizeDeepseekApiKey
} from "./dev-ai-config.mjs";
export {
  parseEnvFileContent,
  parseEnvValue,
  serializeEnvValue
} from "./dev-env-file.mjs";
export {
  buildLocalDatabaseConfig,
  buildLocalSqliteConnectionString,
  isSqliteConnectionString
} from "./dev-local-database.mjs";
export {
  selectPathCommandCandidate
} from "./dev-command-runner.mjs";
export {
  findProcessIdsByCommand,
  isSafeBackendProcessCommand,
  isSafeFrontendProcessCommand,
  resolveUrlPort
} from "./dev-port-processes.mjs";
export {
  describeSandboxRuntime,
  normalizeDockerHost,
  resolveDockerSocketHost
};

const scriptPath = fileURLToPath(import.meta.url);
const repoRoot = path.resolve(path.dirname(scriptPath), "..");
const localEnvPath = path.join(repoRoot, ".env.local");
const backendProjectPath = path.join("Backend", "Backend", "Backend.csproj");
const backendSolutionPath = path.join("Backend", "Backend.sln");
const backendOutPath = path.join(repoRoot, "backend-dev.log");
const backendErrPath = path.join(repoRoot, "backend-dev.err.log");
const npmInstallMarkerFileName = ".ojsharp-package-lock.sha256";
const connectionStringKey = "ConnectionStrings__DefaultConnection";
const frontendUrl = "http://localhost:3000";
const {
  buildChildEnv,
  ensureCommand,
  resolveCommand,
  runCommand,
  spawnCommand
} = createCommandRunner();

const defaultConfig = {
  NEXT_PUBLIC_API_BASE_URL: "http://localhost:5140/api/v1",
  ASPNETCORE_ENVIRONMENT: "Development",
  BackendUrls: "http://localhost:5140",
  Database__Provider: localDatabase.provider,
  Deepseek__BaseUrl: "https://api.deepseek.com",
  Deepseek__Model: "deepseek-v4-flash",
  Deepseek__ThinkingEnabled: "false"
};

const managedConfigKeys = [
  "NEXT_PUBLIC_API_BASE_URL",
  "ASPNETCORE_ENVIRONMENT",
  "BackendUrls",
  "Database__Provider",
  "ConnectionStrings__DefaultConnection",
  "DOCKER_HOST",
  "SeedAdmin__Email",
  "SeedAdmin__Password",
  "Smtp__Host",
  "Smtp__Port",
  "Smtp__EnableSsl",
  "Smtp__Username",
  "Smtp__Password",
  "Smtp__FromAddress",
  "Smtp__FromName",
  "Deepseek__Enabled",
  "Deepseek__ApiKey",
  "Deepseek__BaseUrl",
  "Deepseek__Model",
  "Deepseek__ThinkingEnabled"
];

const requiredLocalConfig = [];

function parseArgs(argv) {
  const args = new Set(argv);
  return {
    backendOnly: args.has("--backend-only"),
    doctor: args.has("--doctor"),
    help: args.has("--help") || args.has("-h"),
    noPrompt: args.has("--no-prompt"),
    setupOnly: args.has("--setup-only"),
    skipInstall: args.has("--skip-install")
  };
}

function usage() {
  return [
    "Usage:",
    "  npm run dev                    restore deps, prompt for local config, start backend + frontend",
    "  npm run dev:setup              restore deps and write .env.local without starting servers",
    "  npm run dev:backend            restore deps, load .env.local, start backend only",
    "  npm run dev:doctor             inspect local prerequisites without writing secrets",
    "",
    "Options:",
    "  --no-prompt                    fail instead of asking for missing local config",
    "  --skip-install                 skip npm ci and dotnet restore",
    "  --setup-only                   configure and restore dependencies only",
    "  --backend-only                 start only the backend",
    "  --doctor                       print local prerequisite and config guidance only",
    "",
    "Environment:",
    "  DOTNET_CLI                     optional absolute path to a dotnet executable",
    "  Deepseek__ApiKey               optional local AI provider key",
    "  Database__Provider             optional external override; local dev defaults to Sqlite",
    "",
    "Guided setup:",
    "  The script creates a gitignored SQLite database file automatically.",
    "  Only the AI key may be requested interactively and written to gitignored .env.local.",
    "  OS/admin prompts are not bypassed; approve them, fix permissions, then rerun npm run dev."
  ].join("\n");
}

export function serializeEnvFile(values) {
  return serializeEnvFileContent(values, managedConfigKeys);
}

export function mergeEffectiveConfig(fileConfig, processConfig = process.env) {
  const hasExplicitProvider = Object.prototype.hasOwnProperty.call(fileConfig, "Database__Provider")
    || Object.prototype.hasOwnProperty.call(processConfig, "Database__Provider");
  const merged = {
    ...defaultConfig,
    ...fileConfig,
    ...Object.fromEntries(
      managedConfigKeys
        .filter((key) => Object.prototype.hasOwnProperty.call(processConfig, key))
        .map((key) => [key, processConfig[key]])
    )
  };

  if (isUsableConfigValue(merged[connectionStringKey])) {
    merged[connectionStringKey] = normalizePostgresConnectionString(merged[connectionStringKey]);
  } else {
    const derivedConnectionString = derivePostgresConnectionFromEnvironment(processConfig);
    if (isUsableConfigValue(derivedConnectionString)) {
      merged[connectionStringKey] = derivedConnectionString;
    }
  }

  if (!hasExplicitProvider && looksLikePostgresConnectionString(merged[connectionStringKey])) {
    merged.Database__Provider = "PostgreSql";
  }

  normalizeEffectiveAiConfig(merged, fileConfig, processConfig);
  return merged;
}

function looksLikePostgresConnectionString(value) {
  const trimmed = String(value ?? "").trim();
  return /^postgres(ql)?:\/\//i.test(trimmed)
    || /(?:^|;)\s*Host\s*=/i.test(trimmed);
}

export function resolveBackendHealthUrl(backendUrls) {
  const firstUrl = String(backendUrls || defaultConfig.BackendUrls)
    .split(";")
    .map((value) => value.trim())
    .find(Boolean) ?? defaultConfig.BackendUrls;
  const url = new URL(firstUrl);
  url.pathname = "/api/v1/health";
  url.search = "";
  url.hash = "";
  return url.toString();
}

export function resolveBackendPort(backendUrls) {
  const firstUrl = String(backendUrls || defaultConfig.BackendUrls)
    .split(";")
    .map((value) => value.trim())
    .find(Boolean) ?? defaultConfig.BackendUrls;
  return resolveUrlPort(firstUrl);
}

export function shouldRunNpmCi(repoDirectory, exists = fs.existsSync) {
  const nodeModulesPath = path.join(repoDirectory, "node_modules");
  const lockPath = path.join(repoDirectory, "package-lock.json");
  const installedLockPath = path.join(nodeModulesPath, ".package-lock.json");
  const installMarkerPath = path.join(nodeModulesPath, npmInstallMarkerFileName);

  if (!exists(nodeModulesPath)) {
    return true;
  }

  if (!exists(lockPath)) {
    return false;
  }

  if (!exists(installedLockPath)) {
    return true;
  }

  if (!exists(installMarkerPath)) {
    return true;
  }

  return readTextFile(installMarkerPath).trim() !== hashFile(lockPath);
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    console.log(usage());
    return;
  }

  ensureNodeVersion();
  process.chdir(repoRoot);

  if (options.doctor) {
    await runDoctor();
    return;
  }

  if (!options.skipInstall) {
    ensureRestoreCommands();
  }

  const fileConfig = await readLocalConfig();
  const config = await ensureLocalConfig(fileConfig, options);
  console.log(`Sandbox runtime: ${describeSandboxRuntime(config)}`);

  if (!options.skipInstall) {
    await restoreDependencies();
  }

  if (options.setupOnly) {
    console.log("Local startup configuration is ready.");
    return;
  }

  await ensureBackendBuildArtifactsAvailable(config);
  await ensureSeedAdmin(config);
  const backend = await ensureBackend(config);
  if (options.backendOnly) {
    if (backend) {
      await waitForChildExit(backend.process);
    }

    return;
  }

  await startFrontend(backend);
}

function ensureNodeVersion() {
  const major = Number.parseInt(process.versions.node.split(".")[0] ?? "0", 10);
  if (major < 20) {
    throw new Error(`Node.js 20 or newer is required. Current version: ${process.version}`);
  }
}

async function readLocalConfig() {
  try {
    const content = await fsp.readFile(localEnvPath, "utf8");
    return parseEnvFileContent(content);
  } catch (exception) {
    if (exception?.code === "ENOENT") {
      return {};
    }

    throw exception;
  }
}

async function runDoctor() {
  const fileConfig = await readLocalConfig();
  const discoveredConfig = await discoverLocalConfig(fileConfig);
  const effectiveConfig = mergeEffectiveConfig({
    ...discoveredConfig,
    ...fileConfig,
    ...buildLocalDatabaseConfig(repoRoot)
  });
  const npmCommand = resolveCommand("npm");
  const dotnetCommand = resolveCommand("dotnet");

  console.log("Local startup doctor");
  console.log("");
  console.log(`Node.js: ${process.version} ${Number.parseInt(process.versions.node.split(".")[0] ?? "0", 10) >= 20 ? "OK" : "needs Node.js 20+"}`);
  console.log(`npm: ${npmCommand ? `OK (${npmCommand})` : "missing"}`);
  console.log(`.NET SDK: ${dotnetCommand ? `OK (${dotnetCommand})` : "missing"}`);
  console.log(`Root npm dependencies: ${shouldRunNpmCi(repoRoot) ? "need restore on next npm run dev" : "ready for current package-lock.json"}`);
  console.log(`Local database: ${describeDatabaseConfig(effectiveConfig)}`);
  console.log(`Sandbox runtime: ${describeSandboxRuntime(effectiveConfig)}`);
  console.log(`Seed admin email: ${describeSeedAdminConfig(effectiveConfig.SeedAdmin__Email, "email")}`);
  console.log(`Seed admin password: ${describeSeedAdminConfig(effectiveConfig.SeedAdmin__Password, "password")}`);
  console.log(`DeepSeek API key: ${describeAiConfig(effectiveConfig)}`);
  console.log("");
  console.log("Next step:");

  const missing = [];
  if (!npmCommand) missing.push("install Node.js 20+ with npm");
  if (!dotnetCommand) missing.push("install .NET 9 SDK or set DOTNET_CLI");
  if (!isExplicitlyDisabled(effectiveConfig.Deepseek__Enabled)
      && !isUsableConfigValue(effectiveConfig.Deepseek__ApiKey)) {
    missing.push("enter Deepseek__ApiKey or leave it blank to disable local AI");
  }

  if (missing.length === 0) {
    console.log("  Run npm run dev.");
  } else {
    for (const item of missing) {
      console.log(`  - ${item}`);
    }

    console.log("");
    console.log("Run npm run dev in PowerShell or your terminal and follow the prompts.");
  }
}

function describeConfigPresence(value) {
  return isUsableConfigValue(String(value ?? "")) ? "configured" : "missing";
}

function describeDatabaseConfig(config) {
  const provider = String(config.Database__Provider ?? localDatabase.provider);
  const connectionString = String(config[connectionStringKey] ?? "");
  if (isSqliteConnectionString(connectionString)) {
    return `${provider} (${connectionString.replace(/^Data Source=/i, "")})`;
  }

  return isUsableConfigValue(connectionString) ? "configured external database" : "will create local SQLite file";
}

function describeSeedAdminConfig(value, kind) {
  if (isUsableConfigValue(String(value ?? ""))) {
    return "configured";
  }

  return kind === "email"
    ? `will use local default ${localSeedAdminDefaults.email}`
    : "will use local default password";
}

function describeAiConfig(config) {
  if (isExplicitlyDisabled(config.Deepseek__Enabled)) {
    return "disabled locally";
  }

  if (!isUsableConfigValue(String(config.Deepseek__ApiKey ?? ""))) {
    return "missing";
  }

  return isAcceptableDeepseekApiKey(config.Deepseek__ApiKey)
    ? "configured"
    : "invalid; rerun npm run dev to repair";
}

function describeSandboxRuntime(config, probe = isDockerRuntimeReachable) {
  const dockerHost = resolveDockerSocketHost(
    config.DOCKER_HOST || process.env.DOCKER_HOST,
    os.homedir(),
    fs.existsSync,
    process.platform);

  if (!dockerHost) {
    return "not detected; workspace Run and Submit stay disabled until a Docker-compatible runtime is available";
  }

  return probe(dockerHost)
    ? `detected (${dockerHost})`
    : `configured but unreachable (${dockerHost}); workspace Run and Submit stay disabled until the runtime is available`;
}

function isDockerRuntimeReachable(dockerHost) {
  const cliDockerHost = dockerHost?.startsWith("npipe://./pipe/")
    ? dockerHost.replace("npipe://./pipe/", "npipe:////./pipe/")
    : dockerHost;
  const result = spawnSync("docker", ["version", "--format", "{{.Server.Version}}"], {
    cwd: repoRoot,
    encoding: "utf8",
    env: {
      ...process.env,
      ...(cliDockerHost ? { DOCKER_HOST: cliDockerHost } : {})
    },
    stdio: ["ignore", "pipe", "ignore"],
    timeout: 5000,
    windowsHide: true
  });

  return result.status === 0 && Boolean(result.stdout.trim());
}

async function ensureLocalConfig(fileConfig, options) {
  const discoveredConfig = await discoverLocalConfig(fileConfig);
  const writableConfig = { ...defaultConfig, ...fileConfig };
  cleanLocalAiConfig(writableConfig);
  const localDatabaseConfig = await ensureLocalDatabaseConfig(repoRoot);
  writableConfig.Database__Provider = localDatabaseConfig.Database__Provider;
  writableConfig[connectionStringKey] = localDatabaseConfig[connectionStringKey];

  const configWithDatabase = mergeEffectiveConfig({ ...discoveredConfig, ...writableConfig });
  if (!isUsableConfigValue(configWithDatabase.SeedAdmin__Email)) {
    writableConfig.SeedAdmin__Email = localSeedAdminDefaults.email;
  }

  if (!isUsableConfigValue(configWithDatabase.SeedAdmin__Password)) {
    writableConfig.SeedAdmin__Password = localSeedAdminDefaults.password;
  }

  const effectiveConfig = mergeEffectiveConfig({ ...discoveredConfig, ...writableConfig });
  const prompts = [];

  for (const item of requiredLocalConfig) {
    if (!item.validate(String(effectiveConfig[item.key] ?? ""))) {
      prompts.push(item);
    }
  }

  const aiDisabledExplicitly = isExplicitlyDisabled(fileConfig.Deepseek__Enabled)
    || isExplicitlyDisabled(process.env.Deepseek__Enabled);
  if (!aiDisabledExplicitly
      && !isAcceptableDeepseekApiKey(effectiveConfig.Deepseek__ApiKey)) {
    prompts.push({
      key: "Deepseek__ApiKey",
      label: "DeepSeek API key (blank disables local AI assistance)",
      secret: true,
      optional: true,
      normalize: normalizeDeepseekApiKey,
      validate: (value) => value.trim() === "" || isAcceptableDeepseekApiKey(value),
      error: "DeepSeek API key must start with sk- and contain one key value. If you pasted it multiple times, paste it once or leave blank to disable local AI."
    });
  }

  if (prompts.length > 0) {
    if (options.noPrompt || !process.stdin.isTTY) {
      const missingKeys = prompts.map((item) => item.key).join(", ");
      throw new Error([
        `Missing local startup configuration: ${missingKeys}.`,
        "Run npm run dev in an interactive terminal, or set these values in .env.local.",
        "",
        "PowerShell guide:",
        "  cd \"C:\\path\\to\\AISD-SS26-Group-7\"",
        "  npm run dev",
        "",
        "The local database is created automatically as a SQLite file under .local-data.",
        "",
        "If a system or permission prompt appears for Node or .NET, approve it and rerun npm run dev."
      ].join("\n"));
    }

    await promptForConfig(prompts, writableConfig);
  }

  cleanLocalAiConfig(writableConfig);
  const effectiveApiKey = process.env.Deepseek__ApiKey ?? discoveredConfig.Deepseek__ApiKey ?? writableConfig.Deepseek__ApiKey;
  const shouldEnableAi = !aiDisabledExplicitly && isAcceptableDeepseekApiKey(effectiveApiKey);
  writableConfig.Deepseek__Enabled = shouldEnableAi
    ? "true"
    : String(writableConfig.Deepseek__Enabled ?? "false");

  const dockerHost = normalizeDockerHost(writableConfig.DOCKER_HOST || detectDockerHostFromContext());
  if (dockerHost) {
    writableConfig.DOCKER_HOST = dockerHost;
  }

  if (isUsableConfigValue(writableConfig[connectionStringKey])) {
    writableConfig[connectionStringKey] = normalizePostgresConnectionString(
      writableConfig[connectionStringKey]
    );
  }

  await fsp.writeFile(localEnvPath, serializeEnvFile(writableConfig), { mode: 0o600 });
  return mergeEffectiveConfig({ ...discoveredConfig, ...writableConfig });
}

async function discoverLocalConfig(fileConfig) {
  const discovered = {};
  const userSecrets = await readDotnetUserSecrets();

  applyDiscoveredValue(discovered, fileConfig, connectionStringKey, process.env[connectionStringKey]);
  applyDiscoveredValue(discovered, fileConfig, connectionStringKey, derivePostgresConnectionFromEnvironment());
  applyDiscoveredValue(discovered, fileConfig, connectionStringKey, userSecrets["ConnectionStrings:DefaultConnection"]);
  applyDiscoveredValue(discovered, fileConfig, connectionStringKey, userSecrets[connectionStringKey]);
  applyDiscoveredValue(discovered, fileConfig, "SeedAdmin__Email", process.env.SeedAdmin__Email);
  applyDiscoveredValue(discovered, fileConfig, "SeedAdmin__Email", userSecrets["SeedAdmin:Email"]);
  applyDiscoveredValue(discovered, fileConfig, "SeedAdmin__Email", userSecrets.SeedAdmin__Email);
  applyDiscoveredValue(discovered, fileConfig, "SeedAdmin__Password", process.env.SeedAdmin__Password);
  applyDiscoveredValue(discovered, fileConfig, "SeedAdmin__Password", userSecrets["SeedAdmin:Password"]);
  applyDiscoveredValue(discovered, fileConfig, "SeedAdmin__Password", userSecrets.SeedAdmin__Password);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Host", process.env.Smtp__Host);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Host", userSecrets["Smtp:Host"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Host", userSecrets.Smtp__Host);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Port", process.env.Smtp__Port);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Port", userSecrets["Smtp:Port"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Port", userSecrets.Smtp__Port);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__EnableSsl", process.env.Smtp__EnableSsl);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__EnableSsl", userSecrets["Smtp:EnableSsl"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__EnableSsl", userSecrets.Smtp__EnableSsl);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Username", process.env.Smtp__Username);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Username", userSecrets["Smtp:Username"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Username", userSecrets.Smtp__Username);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Password", process.env.Smtp__Password);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Password", userSecrets["Smtp:Password"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__Password", userSecrets.Smtp__Password);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__FromAddress", process.env.Smtp__FromAddress);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__FromAddress", userSecrets["Smtp:FromAddress"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__FromAddress", userSecrets.Smtp__FromAddress);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__FromName", process.env.Smtp__FromName);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__FromName", userSecrets["Smtp:FromName"]);
  applyDiscoveredValue(discovered, fileConfig, "Smtp__FromName", userSecrets.Smtp__FromName);
  applyDiscoveredValue(discovered, fileConfig, "Deepseek__ApiKey", process.env.Deepseek__ApiKey);
  applyDiscoveredValue(discovered, fileConfig, "Deepseek__ApiKey", userSecrets["Deepseek:ApiKey"]);
  applyDiscoveredValue(discovered, fileConfig, "Deepseek__ApiKey", userSecrets.Deepseek__ApiKey);

  if (isUsableConfigValue(discovered[connectionStringKey])) {
    discovered[connectionStringKey] = normalizePostgresConnectionString(discovered[connectionStringKey]);
  }

  if (isUsableConfigValue(discovered.Deepseek__ApiKey)
      && !Object.prototype.hasOwnProperty.call(fileConfig, "Deepseek__Enabled")) {
    discovered.Deepseek__Enabled = "true";
  }

  return discovered;
}

function applyDiscoveredValue(target, fileConfig, key, value) {
  if (Object.prototype.hasOwnProperty.call(fileConfig, key)
      || Object.prototype.hasOwnProperty.call(target, key)
      || !isUsableConfigValue(value)) {
    return;
  }

  if (key === connectionStringKey) {
    target[key] = normalizePostgresConnectionString(value);
    return;
  }

  if (key === "Deepseek__ApiKey") {
    const normalizedKey = normalizeDeepseekApiKey(value);
    if (isAcceptableDeepseekApiKey(normalizedKey)) {
      target[key] = normalizedKey;
    }

    return;
  }

  target[key] = String(value).trim();
}

async function readDotnetUserSecrets() {
  const dotnet = resolveCommand("dotnet");
  if (!dotnet) {
    return {};
  }

  const result = spawnSync(dotnet, ["user-secrets", "list", "--project", backendProjectPath], {
    cwd: repoRoot,
    encoding: "utf8",
    env: buildChildEnv()
  });

  if (result.status !== 0) {
    return {};
  }

  return parseDotnetUserSecrets(result.stdout);
}

function isExplicitlyDisabled(value) {
  return String(value ?? "").trim().toLowerCase() === "false";
}

async function promptForConfig(prompts, writableConfig) {
  console.log("Missing local startup configuration. Values will be stored in .env.local, which is gitignored.");
  for (const item of prompts) {
    if (item.help) {
      console.log("");
      console.log(item.help.join("\n"));
    }

    let accepted = false;
    while (!accepted) {
      const rawValue = item.secret
        ? await readSecret(`${item.label}: `)
        : await readLine(`${item.label}: `);
      const value = typeof item.normalize === "function"
        ? item.normalize(rawValue)
        : rawValue.trim();

      if (item.optional && value.trim() === "") {
        writableConfig.Deepseek__Enabled = "false";
        accepted = true;
        continue;
      }

      if (item.validate(value.trim())) {
        writableConfig[item.key] = value.trim();
        accepted = true;
      } else {
        console.log(item.error);
      }
    }
  }
}

async function readLine(prompt) {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  try {
    return await rl.question(prompt);
  } finally {
    rl.close();
  }
}

async function readSecret(prompt) {
  const output = new Writable({
    write(_chunk, _encoding, callback) {
      callback();
    }
  });
  const rl = readline.createInterface({ input: process.stdin, output, terminal: true });
  try {
    process.stdout.write(prompt);
    const value = await rl.question("");
    process.stdout.write("\n");
    return value;
  } finally {
    rl.close();
  }
}

function detectDockerHostFromContext() {
  if (process.env.DOCKER_HOST) {
    return process.env.DOCKER_HOST;
  }

  const result = spawnSync("docker", ["context", "inspect", "--format", "{{.Endpoints.docker.Host}}"], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  if (result.status === 0 && result.stdout.trim()) {
    return result.stdout.trim();
  }

  return resolveDockerSocketHost("", os.homedir(), fs.existsSync, process.platform);
}

function normalizeDockerHost(value) {
  const trimmed = String(value ?? "").trim();
  if (!trimmed.toLowerCase().startsWith("npipe:")) {
    return trimmed;
  }

  return trimmed.replace(/^npipe:\/+(?:\.\/)?pipe\//i, "npipe://./pipe/");
}

function resolveDockerSocketHost(value, homeDirectory, exists, platform) {
  const normalized = normalizeDockerHost(value);
  if (normalized) {
    return normalized;
  }

  if (platform === "win32") {
    return "";
  }

  const candidates = [
    "/var/run/docker.sock",
    path.join(homeDirectory, ".docker", "run", "docker.sock"),
    path.join(homeDirectory, ".docker", "desktop", "docker.sock"),
    path.join(homeDirectory, ".colima", "default", "docker.sock")
  ];
  const socketPath = candidates.find((candidate) => exists(candidate));
  return socketPath ? `unix://${socketPath}` : "";
}

async function restoreDependencies() {
  ensureRestoreCommands();

  if (shouldRunNpmCi(repoRoot)) {
    await runCommand("npm", ["ci"], { stdio: "inherit" });
  }

  await writeNpmInstallMarker();
  console.log("Node dependencies are ready for the current lockfile.");

  await runCommand("dotnet", ["restore", backendSolutionPath], {
    env: buildChildEnv(),
    stdio: "inherit"
  });
}

export function buildBackendRunArgs(extraArgs = []) {
  const args = ["run", "--project", backendProjectPath];
  return extraArgs.length > 0
    ? [...args, "--", ...extraArgs]
    : args;
}

async function ensureSeedAdmin(config) {
  console.log("Ensuring local seed administrator is ready ...");
  await runCommand("dotnet", buildBackendRunArgs(["--seed-admin-only"]), {
    cwd: repoRoot,
    env: buildChildEnv(config),
    stdio: "inherit"
  });
}

async function ensureBackendBuildArtifactsAvailable(config) {
  const backendProcessIds = findRepoBackendProcessIds(config);
  if (backendProcessIds.length === 0) {
    return;
  }

  console.log("Stopping old local backend process before rebuilding ...");
  stopProcessIds(backendProcessIds);

  const port = resolveBackendPort(config.BackendUrls);
  await waitForPortToClose(port, 10);
}

async function writeNpmInstallMarker() {
  const markerPath = path.join(repoRoot, "node_modules", npmInstallMarkerFileName);
  await fsp.mkdir(path.dirname(markerPath), { recursive: true });
  await fsp.writeFile(markerPath, `${hashFile(path.join(repoRoot, "package-lock.json"))}\n`);
}

function hashFile(filePath) {
  return createHash("sha256").update(fs.readFileSync(filePath)).digest("hex");
}

function readTextFile(filePath) {
  try {
    return fs.readFileSync(filePath, "utf8");
  } catch {
    return "";
  }
}

function ensureRestoreCommands() {
  ensureCommand("npm", [
    "Install Node.js 20 LTS or newer, reopen the terminal, then rerun npm run dev.",
    "Windows: install from https://nodejs.org/ and choose the npm option.",
    "macOS: brew install node or use the official installer."
  ].join("\n"));
  ensureCommand("dotnet", [
    "Install the .NET SDK that supports net9.0, reopen the terminal, then rerun npm run dev.",
    "Windows: install .NET 9 SDK from https://dotnet.microsoft.com/download.",
    "macOS Homebrew: brew install dotnet@9, or set DOTNET_CLI to the dotnet executable path."
  ].join("\n"));
}

async function ensureBackend(config) {
  const healthUrl = resolveBackendHealthUrl(config.BackendUrls);
  if (await isBackendHealthy(healthUrl)) {
    const safeProcessIds = findSafeBackendProcessIds(config);
    if (safeProcessIds.length > 0) {
      if (await canLoginWithSeedAdmin(config)) {
        console.log("Restarting existing local backend so it uses the current checkout ...");
      } else {
        console.log("Backend health is reachable, but the configured seed administrator cannot sign in.");
        console.log("Restarting the local backend on this port so it uses the current one-command startup config ...");
      }

      stopProcessIds(safeProcessIds);
      await waitForBackendToStop(healthUrl, 10);
    } else if (await canLoginWithSeedAdmin(config)) {
      console.log(`Backend is already running at ${healthUrl}.`);
      return null;
    } else {
      throw new Error([
        `Port ${resolveBackendPort(config.BackendUrls)} is already serving a backend health response, but it does not accept the configured seed administrator.`,
        "The startup script could not identify a safe local Backend process to restart automatically.",
        "",
        "Close the process using this port, then rerun npm run dev."
      ].join("\n"));
    }
  }

  if (await isBackendHealthy(healthUrl)) {
    if (await canLoginWithSeedAdmin(config)) {
      console.log(`Backend is already running at ${healthUrl}.`);
      return null;
    }

    throw new Error([
      "Backend is still reachable after attempting to restart the incompatible local process.",
      "It still does not accept the configured seed administrator.",
      "Close the old backend process manually, then rerun npm run dev."
    ].join("\n"));
  }

  console.log(`Starting backend on ${config.BackendUrls} ...`);
  console.log(`Backend logs: ${backendOutPath}`);

  const out = fs.openSync(backendOutPath, "a");
  const err = fs.openSync(backendErrPath, "a");
  const backend = spawnCommand("dotnet", buildBackendRunArgs(), {
    cwd: repoRoot,
    env: buildChildEnv(config),
    stdio: ["ignore", out, err]
  });

  const started = await waitForBackend(backend, healthUrl, 30);
  if (!started) {
    const errorTail = await readTail(backendErrPath, 40);
    if (!backend.killed && backend.exitCode === null) {
      backend.kill();
    }

    throw new Error([
      "Backend did not become healthy.",
      `Health URL: ${healthUrl}`,
      "Last backend error log lines:",
      errorTail || "(no backend error log output)",
      "",
      ...diagnoseBackendFailure(errorTail, config)
    ].join("\n"));
  }

  return { process: backend };
}

async function isBackendHealthy(healthUrl) {
  try {
    const response = await fetch(healthUrl, { signal: AbortSignal.timeout(2000) });
    return response.ok;
  } catch {
    return false;
  }
}

async function canLoginWithSeedAdmin(config) {
  const email = String(config.SeedAdmin__Email ?? "").trim();
  const password = String(config.SeedAdmin__Password ?? "").trim();
  if (!email || !password) {
    return false;
  }

  try {
    const response = await fetch(resolveBackendAuthLoginUrl(config.BackendUrls), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
      signal: AbortSignal.timeout(3000)
    });

    if (!response.ok) {
      return false;
    }

    const payload = await response.json();
    return payload?.ok === true
      && payload?.data?.token
      && payload?.data?.user?.email === email;
  } catch {
    return false;
  }
}

function resolveBackendAuthLoginUrl(backendUrls) {
  const url = new URL(resolveBackendHealthUrl(backendUrls));
  url.pathname = "/api/v1/auth/login";
  return url.toString();
}

function findSafeBackendProcessIds(config) {
  const port = resolveBackendPort(config.BackendUrls);
  const processIds = findListeningProcessIds(port);
  return processIds.filter((processId) => {
    const command = readProcessCommand(processId);
    return isRepoBackendProcessCommand(command);
  });
}

function findRepoBackendProcessIds(config) {
  const port = resolveBackendPort(config.BackendUrls);
  const listeningProcessIds = new Set(findListeningProcessIds(port));
  const processIds = findProcessIdsByCommand(isRepoBackendProcessCommand);

  return [...new Set([...listeningProcessIds, ...processIds])]
    .filter((processId) => processId !== process.pid)
    .filter((processId) => {
      const command = readProcessCommand(processId);
      return isRepoBackendProcessCommand(command);
    });
}

function isRepoBackendProcessCommand(command) {
  const normalizedCommand = normalizePathText(command);
  return isSafeBackendProcessCommand(command)
    && normalizedCommand.includes(normalizePathText(repoRoot));
}

function normalizePathText(value) {
  return String(value ?? "").replace(/\\/g, "/").toLowerCase();
}

async function waitForBackendToStop(healthUrl, seconds) {
  for (let attempt = 0; attempt < seconds; attempt += 1) {
    if (!await isBackendHealthy(healthUrl)) {
      return;
    }

    await delay(1000);
  }
}

async function waitForPortToClose(port, seconds) {
  for (let attempt = 0; attempt < seconds; attempt += 1) {
    if (findListeningProcessIds(port).length === 0) {
      return;
    }

    await delay(1000);
  }
}

async function waitForBackend(backend, healthUrl, seconds) {
  for (let attempt = 0; attempt < seconds; attempt += 1) {
    if (backend.exitCode !== null) {
      return false;
    }

    if (await isBackendHealthy(healthUrl)) {
      return true;
    }

    await delay(1000);
  }

  return false;
}

async function startFrontend(backend) {
  await ensureFrontendPortAvailable();
  console.log(`Starting frontend on ${frontendUrl} ...`);
  console.log(`Open the app: ${frontendUrl}`);
  const frontend = spawnCommand("npm", ["run", "dev:frontend"], {
    cwd: repoRoot,
    env: process.env,
    stdio: "inherit"
  });

  const stopChildren = () => {
    if (frontend.exitCode === null) {
      frontend.kill();
    }

    if (backend?.process && backend.process.exitCode === null) {
      backend.process.kill();
    }
  };

  process.once("SIGINT", () => {
    stopChildren();
    process.exit(130);
  });
  process.once("SIGTERM", () => {
    stopChildren();
    process.exit(143);
  });

  const exitCode = await waitForChildExit(frontend);
  if (backend?.process && backend.process.exitCode === null) {
    backend.process.kill();
  }

  process.exitCode = exitCode ?? 0;
}

async function ensureFrontendPortAvailable() {
  const port = resolveUrlPort(frontendUrl);
  const processIds = findListeningProcessIds(port);
  if (processIds.length === 0) {
    return;
  }

  const safeProcessIds = processIds.filter((processId) => {
    const command = readProcessCommand(processId);
    return isSafeFrontendProcessCommand(command);
  });

  if (safeProcessIds.length === 0) {
    throw new Error([
      `Port ${port} is already in use, but the startup script could not identify it as a local Next.js process.`,
      "Close the process using this port, then rerun npm run dev."
    ].join("\n"));
  }

  console.log(`Restarting old local frontend process on port ${port} ...`);
  stopProcessIds(safeProcessIds);

  await waitForPortToClose(port, 10);

  if (findListeningProcessIds(port).length > 0) {
    throw new Error([
      `Port ${port} is still in use after attempting to stop the old local frontend.`,
      "Close the process manually, then rerun npm run dev."
    ].join("\n"));
  }
}

function waitForChildExit(child) {
  if (!child) {
    return new Promise(() => {});
  }

  return new Promise((resolve, reject) => {
    child.once("error", (exception) => {
      reject(new Error(`Failed to start child process: ${exception.message}`));
    });
    child.on("exit", (code) => resolve(code));
  });
}

async function readTail(filePath, lineCount) {
  try {
    const content = await fsp.readFile(filePath, "utf8");
    return content.split(/\r?\n/).slice(-lineCount).join("\n").trim();
  } catch {
    return "";
  }
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  main().catch((exception) => {
    console.error(exception.message);
    process.exitCode = 1;
  });
}
