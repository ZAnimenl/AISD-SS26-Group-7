#!/usr/bin/env node

import { spawn, spawnSync } from "node:child_process";
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
  getDockerDaemonStatus,
  isDatabaseStartupFailure,
  isLocalDatabaseTarget,
  localPostgres,
  localSeedAdminDefaults,
  tryProvisionLocalPostgres
} from "./dev-postgres.mjs";

export {
  convertPostgresUrlToNpgsql,
  diagnoseBackendFailure,
  isUsableConfigValue,
  parseDotnetUserSecrets
} from "./dev-support.mjs";
export {
  buildLocalPostgresConnectionString,
  isDatabaseStartupFailure,
  isDockerCredentialHelperFailure,
  isLocalDatabaseTarget,
  parseDockerPortOutput
} from "./dev-postgres.mjs";

const scriptPath = fileURLToPath(import.meta.url);
const repoRoot = path.resolve(path.dirname(scriptPath), "..");
const localEnvPath = path.join(repoRoot, ".env.local");
const backendProjectPath = path.join("Backend", "Backend", "Backend.csproj");
const backendSolutionPath = path.join("Backend", "Backend.sln");
const backendOutPath = path.join(repoRoot, "backend-dev.log");
const backendErrPath = path.join(repoRoot, "backend-dev.err.log");
const npmInstallMarkerFileName = ".ojsharp-package-lock.sha256";
const resolvedCommands = new Map();
const connectionStringKey = "ConnectionStrings__DefaultConnection";

const defaultConfig = {
  NEXT_PUBLIC_API_BASE_URL: "http://localhost:5140/api/v1",
  ASPNETCORE_ENVIRONMENT: "Development",
  BackendUrls: "http://localhost:5140",
  Deepseek__BaseUrl: "https://api.deepseek.com",
  Deepseek__Model: "deepseek-v4-flash",
  Deepseek__ThinkingEnabled: "false"
};

const managedConfigKeys = [
  "NEXT_PUBLIC_API_BASE_URL",
  "ASPNETCORE_ENVIRONMENT",
  "BackendUrls",
  "ConnectionStrings__DefaultConnection",
  "DOCKER_HOST",
  "SeedAdmin__Email",
  "SeedAdmin__Password",
  "Deepseek__Enabled",
  "Deepseek__ApiKey",
  "Deepseek__BaseUrl",
  "Deepseek__Model",
  "Deepseek__ThinkingEnabled"
];

const requiredLocalConfig = [
  {
    key: connectionStringKey,
    label: "PostgreSQL connection string or URL",
    secret: true,
    normalize: normalizePostgresConnectionString,
    validate: (value) => isUsableConfigValue(value),
    help: [
      "Docker was not available for automatic local PostgreSQL setup.",
      "If you intentionally use an existing PostgreSQL server, paste either format:",
      "  postgresql://postgres:YOUR_PASSWORD@localhost:5432/aisd_ss26_group_7",
      "  Host=localhost;Port=5432;Database=aisd_ss26_group_7;Username=postgres;Password=YOUR_PASSWORD"
    ],
    error: "A real PostgreSQL connection string or URL is required for every backend run."
  },
  {
    key: "SeedAdmin__Email",
    label: "Seed administrator email",
    secret: false,
    validate: (value) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value),
    error: "SeedAdmin__Email must be a real email address."
  },
  {
    key: "SeedAdmin__Password",
    label: "Seed administrator password",
    secret: true,
    validate: (value) => isUsableConfigValue(value) && value.length >= 8,
    error: "SeedAdmin__Password must be at least 8 characters."
  }
];

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
    "  DATABASE_URL                   optional PostgreSQL URL; converted to backend format",
    "  PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD",
    "                                optional PostgreSQL variables; converted when complete",
    "",
    "Guided setup:",
    "  The script reuses .env.local, shell env, DATABASE_URL/PG* values, and .NET user-secrets.",
    "  Missing secrets are requested interactively and written only to gitignored .env.local.",
    "  OS/admin prompts are not bypassed; approve them, fix permissions, then rerun npm run dev."
  ].join("\n");
}

export function parseEnvFileContent(content) {
  const result = {};
  for (const rawLine of content.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) {
      continue;
    }

    const separatorIndex = line.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = line.slice(0, separatorIndex).trim();
    const rawValue = line.slice(separatorIndex + 1).trim();
    if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(key)) {
      continue;
    }

    result[key] = parseEnvValue(rawValue);
  }

  return result;
}

export function parseEnvValue(rawValue) {
  if (rawValue.startsWith("\"") && rawValue.endsWith("\"")) {
    try {
      return JSON.parse(rawValue);
    } catch {
      return rawValue.slice(1, -1);
    }
  }

  if (rawValue.startsWith("'") && rawValue.endsWith("'")) {
    return rawValue.slice(1, -1);
  }

  const hashIndex = rawValue.indexOf("#");
  return (hashIndex === -1 ? rawValue : rawValue.slice(0, hashIndex)).trim();
}

export function serializeEnvFile(values) {
  const orderedKeys = [
    ...managedConfigKeys,
    ...Object.keys(values)
      .filter((key) => !managedConfigKeys.includes(key))
      .sort((left, right) => left.localeCompare(right))
  ];
  const lines = [
    "# Local startup configuration generated by scripts/dev.mjs.",
    "# This file may contain secrets and must stay untracked.",
    ""
  ];

  for (const key of orderedKeys) {
    if (!Object.prototype.hasOwnProperty.call(values, key)) {
      continue;
    }

    const value = values[key];
    if (value === undefined || value === null) {
      continue;
    }

    lines.push(`${key}=${serializeEnvValue(String(value))}`);
  }

  lines.push("");
  return lines.join("\n");
}

export function serializeEnvValue(value) {
  if (/[\r\n]/.test(value)) {
    throw new Error("Environment values must be single-line strings.");
  }

  if (value === "" || /^\s|\s$|[\s#"']/.test(value)) {
    return JSON.stringify(value);
  }

  return value;
}

export function mergeEffectiveConfig(fileConfig, processConfig = process.env) {
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

  return merged;
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

  if (!options.skipInstall) {
    await restoreDependencies();
  }

  if (options.setupOnly) {
    console.log("Local startup configuration is ready.");
    return;
  }

  const backend = await ensureBackendWithLocalDatabaseRepair(config, fileConfig);
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
  const effectiveConfig = mergeEffectiveConfig({ ...discoveredConfig, ...fileConfig });
  const npmCommand = resolveCommand("npm");
  const dotnetCommand = resolveCommand("dotnet");
  const dockerCommand = resolveCommand("docker");
  const dockerDaemonStatus = getDockerDaemonStatus(dockerCommand, repoRoot);
  const canAutoProvisionDatabase = !isUsableConfigValue(effectiveConfig[connectionStringKey])
    && dockerDaemonStatus.startsWith("running");

  console.log("Local startup doctor");
  console.log("");
  console.log(`Node.js: ${process.version} ${Number.parseInt(process.versions.node.split(".")[0] ?? "0", 10) >= 20 ? "OK" : "needs Node.js 20+"}`);
  console.log(`npm: ${npmCommand ? `OK (${npmCommand})` : "missing"}`);
  console.log(`.NET SDK: ${dotnetCommand ? `OK (${dotnetCommand})` : "missing"}`);
  console.log(`Root npm dependencies: ${shouldRunNpmCi(repoRoot) ? "need restore on next npm run dev" : "ready for current package-lock.json"}`);
  console.log(`Docker CLI: ${dockerCommand ? `found (${dockerCommand})` : "missing or not in PATH"}`);
  console.log(`Docker daemon: ${dockerDaemonStatus}`);
  console.log(`PostgreSQL config: ${describeDatabaseConfig(effectiveConfig, canAutoProvisionDatabase)}`);
  console.log(`Seed admin email: ${describeSeedAdminConfig(effectiveConfig.SeedAdmin__Email, "email")}`);
  console.log(`Seed admin password: ${describeSeedAdminConfig(effectiveConfig.SeedAdmin__Password, "password")}`);
  console.log(`DeepSeek API key: ${describeAiConfig(effectiveConfig)}`);
  console.log("");
  console.log("Next step:");

  const missing = [];
  if (!npmCommand) missing.push("install Node.js 20+ with npm");
  if (!dotnetCommand) missing.push("install .NET 9 SDK or set DOTNET_CLI");
  if (!isUsableConfigValue(effectiveConfig[connectionStringKey]) && !canAutoProvisionDatabase) {
    missing.push("start Docker Desktop/Colima or enter PostgreSQL connection info");
  }
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

function describeDatabaseConfig(config, canAutoProvisionDatabase) {
  if (isUsableConfigValue(config[connectionStringKey])) {
    return "configured";
  }

  return canAutoProvisionDatabase
    ? `will auto-provision Docker container ${localPostgres.containerName}`
    : "missing";
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

  return describeConfigPresence(config.Deepseek__ApiKey);
}

async function ensureLocalConfig(fileConfig, options) {
  const discoveredConfig = await discoverLocalConfig(fileConfig);
  const writableConfig = { ...defaultConfig, ...fileConfig };

  if (!isUsableConfigValue(mergeEffectiveConfig({ ...discoveredConfig, ...writableConfig })[connectionStringKey])) {
    const localPostgresConfig = await tryProvisionLocalPostgres({
      delay,
      docker: resolveCommand("docker"),
      repoRoot,
      runCommand
    });
    if (isUsableConfigValue(localPostgresConfig[connectionStringKey])) {
      writableConfig[connectionStringKey] = localPostgresConfig[connectionStringKey];
    }
  }

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
      && !isUsableConfigValue(String(effectiveConfig.Deepseek__ApiKey ?? ""))) {
    prompts.push({
      key: "Deepseek__ApiKey",
      label: "DeepSeek API key (blank disables local AI assistance)",
      secret: true,
      optional: true,
      validate: () => true,
      error: ""
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
        "When Docker is running, local PostgreSQL is created automatically; otherwise the PostgreSQL prompt accepts either:",
        "  postgresql://postgres:YOUR_PASSWORD@localhost:5432/aisd_ss26_group_7",
        "  Host=localhost;Port=5432;Database=aisd_ss26_group_7;Username=postgres;Password=YOUR_PASSWORD",
        "",
        "If a system or permission prompt appears for Node, .NET, Docker Desktop, or Colima, approve it and rerun npm run dev.",
        "Remote PostgreSQL targets are not overwritten automatically; repair remote databases manually."
      ].join("\n"));
    }

    await promptForConfig(prompts, writableConfig);
  }

  const effectiveApiKey = process.env.Deepseek__ApiKey ?? discoveredConfig.Deepseek__ApiKey ?? writableConfig.Deepseek__ApiKey;
  const shouldEnableAi = !aiDisabledExplicitly && isUsableConfigValue(String(effectiveApiKey ?? ""));
  writableConfig.Deepseek__Enabled = shouldEnableAi
    ? "true"
    : String(writableConfig.Deepseek__Enabled ?? "false");

  const dockerHost = writableConfig.DOCKER_HOST || detectDockerHostFromContext();
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

async function ensureBackendWithLocalDatabaseRepair(config, fileConfig) {
  try {
    return await ensureBackend(config);
  } catch (exception) {
    const repairedConfig = await tryRepairLocalDatabaseStartup(exception, config, fileConfig);
    if (!repairedConfig) {
      throw exception;
    }

    console.log("Retrying backend with the project-owned local PostgreSQL database ...");
    return ensureBackend(repairedConfig);
  }
}

async function tryRepairLocalDatabaseStartup(exception, config, fileConfig) {
  if (!isDatabaseStartupFailure(exception?.message)
      || !isLocalDatabaseTarget(config[connectionStringKey])) {
    return null;
  }

  console.log("Local PostgreSQL startup failed; preparing a project-owned Docker PostgreSQL database with default demo credentials.");
  const localPostgresConfig = await tryProvisionLocalPostgres({
    delay,
    docker: resolveCommand("docker"),
    repoRoot,
    runCommand
  });

  if (!isUsableConfigValue(localPostgresConfig[connectionStringKey])) {
    return null;
  }

  const writableConfig = {
    ...defaultConfig,
    ...fileConfig,
    ...localPostgresConfig
  };
  const repairedConfig = {
    ...config,
    ...writableConfig
  };

  if (!isUsableConfigValue(repairedConfig.SeedAdmin__Email)) {
    writableConfig.SeedAdmin__Email = localSeedAdminDefaults.email;
    repairedConfig.SeedAdmin__Email = localSeedAdminDefaults.email;
  }

  if (!isUsableConfigValue(repairedConfig.SeedAdmin__Password)) {
    writableConfig.SeedAdmin__Password = localSeedAdminDefaults.password;
    repairedConfig.SeedAdmin__Password = localSeedAdminDefaults.password;
  }

  const dockerHost = writableConfig.DOCKER_HOST || detectDockerHostFromContext();
  if (dockerHost) {
    writableConfig.DOCKER_HOST = dockerHost;
    repairedConfig.DOCKER_HOST = dockerHost;
  }

  await fsp.writeFile(localEnvPath, serializeEnvFile(writableConfig), { mode: 0o600 });
  return repairedConfig;
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

  target[key] = key === connectionStringKey
    ? normalizePostgresConnectionString(value)
    : String(value).trim();
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

  const colimaSocket = path.join(os.homedir(), ".colima", "default", "docker.sock");
  if (fs.existsSync(colimaSocket)) {
    return `unix://${colimaSocket}`;
  }

  return "";
}

async function restoreDependencies() {
  ensureRestoreCommands();

  if (shouldRunNpmCi(repoRoot)) {
    await runCommand(resolveCommand("npm"), ["ci"], { stdio: "inherit" });
  }

  await writeNpmInstallMarker();
  console.log("Node dependencies are ready for the current lockfile.");

  await runCommand(resolveCommand("dotnet"), ["restore", backendSolutionPath], {
    env: buildChildEnv(),
    stdio: "inherit"
  });
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

function ensureCommand(command, guidance) {
  const resolved = resolveCommand(command);

  if (!resolved) {
    throw new Error(`${command} is required but was not found.\n${guidance}`);
  }
}

function resolveCommand(command) {
  if (resolvedCommands.has(command)) {
    return resolvedCommands.get(command);
  }

  const resolved = command === "dotnet"
    ? resolveDotnetCommand()
    : resolvePathCommand(command);
  resolvedCommands.set(command, resolved);
  return resolved;
}

function resolvePathCommand(command) {
  const checker = process.platform === "win32"
    ? spawnSync("where", [command], { encoding: "utf8" })
    : spawnSync("sh", ["-c", `command -v ${command}`], { encoding: "utf8" });

  if (checker.status !== 0) {
    return "";
  }

  return checker.stdout.trim().split(/\r?\n/)[0] || command;
}

function resolveDotnetCommand() {
  const candidates = [
    process.env.DOTNET_CLI,
    resolvePathCommand("dotnet"),
    "/opt/homebrew/opt/dotnet@9/libexec/dotnet",
    "/opt/homebrew/opt/dotnet/libexec/dotnet",
    "/usr/local/share/dotnet/dotnet",
    "/usr/local/bin/dotnet"
  ].filter(Boolean);

  for (const candidate of candidates) {
    const result = spawnSync(candidate, ["--version"], {
      env: buildChildEnv(undefined, candidate),
      stdio: "ignore"
    });
    if (result.status === 0) {
      return candidate;
    }
  }

  return "";
}

function buildChildEnv(config = {}, dotnetCommand = resolveCommand("dotnet")) {
  const env = { ...process.env, ...config };
  const dotnetRoot = resolveDotnetRoot(dotnetCommand);
  if (dotnetRoot && !env.DOTNET_ROOT) {
    env.DOTNET_ROOT = dotnetRoot;
  }

  return env;
}

function resolveDotnetRoot(dotnetCommand) {
  if (!dotnetCommand || dotnetCommand === "dotnet" || !path.isAbsolute(dotnetCommand)) {
    return "";
  }

  const directory = path.dirname(dotnetCommand);
  return path.basename(directory) === "libexec" ? directory : "";
}

async function ensureBackend(config) {
  const healthUrl = resolveBackendHealthUrl(config.BackendUrls);
  if (await isBackendHealthy(healthUrl)) {
    console.log(`Backend is already running at ${healthUrl}.`);
    return null;
  }

  console.log(`Starting backend on ${config.BackendUrls} ...`);
  console.log(`Backend logs: ${backendOutPath}`);

  const out = fs.openSync(backendOutPath, "a");
  const err = fs.openSync(backendErrPath, "a");
  const backend = spawn(resolveCommand("dotnet"), ["run", "--project", backendProjectPath], {
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
  console.log("Starting frontend on http://localhost:3000 ...");
  const frontend = spawn(resolveCommand("npm"), ["run", "dev:frontend"], {
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

function waitForChildExit(child) {
  if (!child) {
    return new Promise(() => {});
  }

  return new Promise((resolve) => {
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

function runCommand(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: repoRoot, ...options });
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`${command} ${args.join(" ")} failed with exit code ${code}.`));
      }
    });
  });
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
