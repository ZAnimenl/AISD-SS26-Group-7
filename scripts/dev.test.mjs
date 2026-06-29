import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  buildBackendRunArgs,
  buildLocalDatabaseConfig,
  buildLocalSqliteConnectionString,
  cleanLocalAiConfig,
  convertPostgresUrlToNpgsql,
  describeSandboxRuntime,
  diagnoseBackendFailure,
  isAcceptableDeepseekApiKey,
  isSafeBackendProcessCommand,
  isSafeFrontendProcessCommand,
  isSqliteConnectionString,
  isUsableConfigValue,
  mergeEffectiveConfig,
  normalizeDockerHost,
  normalizeDeepseekApiKey,
  resolveBackendPort,
  resolveDockerSocketHost,
  parseEnvFileContent,
  parseDotnetUserSecrets,
  resolveBackendHealthUrl,
  resolveUrlPort,
  resolveNpmCliPath,
  sanitizeChildEnv,
  selectPathCommandCandidate,
  serializeEnvFile,
  shouldRunNpmCi
} from "./dev.mjs";

test("parseEnvFileContent reads quoted and plain local values", () => {
  const parsed = parseEnvFileContent([
    "# local config",
    "BackendUrls=http://localhost:5140",
    "SeedAdmin__Password=\"secret # with hash\"",
    "Deepseek__ApiKey='test-local-key'",
    "BROKEN LINE"
  ].join("\n"));

  assert.equal(parsed.BackendUrls, "http://localhost:5140");
  assert.equal(parsed.SeedAdmin__Password, "secret # with hash");
  assert.equal(parsed.Deepseek__ApiKey, "test-local-key");
  assert.equal(parsed.BROKEN, undefined);
});

test("serializeEnvFile retains SMTP settings for local mail delivery", () => {
  const serialized = serializeEnvFile({
    Smtp__Host: "smtp.example.test",
    Smtp__Port: "587",
    Smtp__EnableSsl: "true",
    Smtp__Username: "mailer",
    Smtp__Password: "app-password",
    Smtp__FromAddress: "mailer@example.test",
    Smtp__FromName: "Assessment Mailer"
  });

  assert.match(serialized, /^Smtp__Host=smtp\.example\.test$/m);
  assert.match(serialized, /^Smtp__Port=587$/m);
  assert.match(serialized, /^Smtp__EnableSsl=true$/m);
  assert.match(serialized, /^Smtp__Username=mailer$/m);
  assert.match(serialized, /^Smtp__Password=app-password$/m);
  assert.match(serialized, /^Smtp__FromAddress=mailer@example\.test$/m);
  assert.match(serialized, /^Smtp__FromName="Assessment Mailer"$/m);
});

test("serializeEnvFile preserves unknown keys and protects values that need quoting", () => {
  const serialized = serializeEnvFile({
    SeedAdmin__Password: "secret # with hash",
    CUSTOM_VALUE: "kept"
  });
  const parsed = parseEnvFileContent(serialized);

  assert.equal(parsed.SeedAdmin__Password, "secret # with hash");
  assert.equal(parsed.CUSTOM_VALUE, "kept");
});

test("isUsableConfigValue rejects empty placeholders", () => {
  assert.equal(isUsableConfigValue("Host=localhost;Database=ojsharp"), true);
  assert.equal(isUsableConfigValue("<real-postgresql-connection-string>"), false);
  assert.equal(isUsableConfigValue("changeme"), false);
  assert.equal(isUsableConfigValue(""), false);
});

test("mergeEffectiveConfig lets explicit process config override local file config", () => {
  const key = "sk-1234567890abcdef1234567890abcdef";
  const merged = mergeEffectiveConfig(
    { BackendUrls: "http://localhost:5140", Deepseek__ApiKey: key },
    { BackendUrls: "http://127.0.0.1:6000" }
  );

  assert.equal(merged.BackendUrls, "http://127.0.0.1:6000");
  assert.equal(merged.Deepseek__ApiKey, key);
  assert.equal(merged.NEXT_PUBLIC_API_BASE_URL, "http://localhost:5140/api/v1");
});

test("mergeEffectiveConfig lets SMTP environment settings override local settings", () => {
  const merged = mergeEffectiveConfig(
    { Smtp__Host: "smtp.local.test", Smtp__Password: "old-password" },
    { Smtp__Host: "smtp.deployment.test", Smtp__Password: "new-password" }
  );

  assert.equal(merged.Smtp__Host, "smtp.deployment.test");
  assert.equal(merged.Smtp__Password, "new-password");
});

test("normalizeDeepseekApiKey collapses accidental repeated pastes", () => {
  const key = "sk-1234567890abcdef1234567890abcdef";

  assert.equal(normalizeDeepseekApiKey(`${key}${key}${key}`), key);
  assert.equal(normalizeDeepseekApiKey(` ${key}\n`), key);
  assert.equal(isAcceptableDeepseekApiKey(key), true);
  assert.equal(isAcceptableDeepseekApiKey(`${key}sk-different1234567890abcdef`), false);
});

test("normalizeDockerHost accepts Docker Desktop npipe variants", () => {
  assert.equal(
    normalizeDockerHost("npipe:////./pipe/dockerDesktopLinuxEngine"),
    "npipe://./pipe/dockerDesktopLinuxEngine"
  );
  assert.equal(
    normalizeDockerHost("npipe:////pipe/docker_engine"),
    "npipe://./pipe/docker_engine"
  );
  assert.equal(
    normalizeDockerHost("unix:///var/run/docker.sock"),
    "unix:///var/run/docker.sock"
  );
});

test("sanitizeChildEnv removes macOS malloc debug flags before spawning child processes", () => {
  const sanitized = sanitizeChildEnv({
    PATH: "/usr/local/bin",
    MallocStackLogging: "0",
    MallocStackLoggingNoCompact: "1",
    MallocScribble: "1"
  });

  assert.equal(sanitized.PATH, "/usr/local/bin");
  assert.equal(sanitized.MallocStackLogging, undefined);
  assert.equal(sanitized.MallocStackLoggingNoCompact, undefined);
  assert.equal(sanitized.MallocScribble, undefined);
});

test("resolveDockerSocketHost detects Docker Desktop socket under home", () => {
  const home = path.join(os.tmpdir(), "ojsharp-home");
  const socket = path.join(home, ".docker", "run", "docker.sock");

  assert.equal(
    resolveDockerSocketHost("", home, (candidate) => candidate === socket, "darwin"),
    `unix://${socket}`
  );
});

test("resolveDockerSocketHost detects Colima socket under home", () => {
  const home = path.join(os.tmpdir(), "ojsharp-home");
  const socket = path.join(home, ".colima", "default", "docker.sock");

  assert.equal(
    resolveDockerSocketHost("", home, (candidate) => candidate === socket, "darwin"),
    `unix://${socket}`
  );
});

test("resolveDockerSocketHost keeps explicit Docker host values", () => {
  assert.equal(
    resolveDockerSocketHost(" unix:///custom/docker.sock ", "/unused", () => false, "darwin"),
    "unix:///custom/docker.sock"
  );
});

test("cleanLocalAiConfig removes stale LocalLlm provider settings", () => {
  const key = "sk-1234567890abcdef1234567890abcdef";
  const config = cleanLocalAiConfig({
    Deepseek__ApiKey: `${key}${key}`,
    LocalLlm__Enabled: "true",
    LocalLlm__ApiKey: key,
    LocalLlm__BaseUrl: "https://api.deepseek.com",
    LocalLlm__Model: "deepseek-chat"
  });

  assert.equal(config.Deepseek__ApiKey, key);
  assert.equal(config.LocalLlm__Enabled, "false");
  assert.equal(config.LocalLlm__ApiKey, undefined);
  assert.equal(config.LocalLlm__BaseUrl, undefined);
  assert.equal(config.LocalLlm__Model, undefined);
});

test("mergeEffectiveConfig ignores invalid process AI key and disables LocalLlm", () => {
  const key = "sk-1234567890abcdef1234567890abcdef";
  const merged = mergeEffectiveConfig(
    {
      Deepseek__ApiKey: key,
      LocalLlm__Enabled: "true",
      LocalLlm__ApiKey: key
    },
    {
      Deepseek__ApiKey: `${key}sk-different1234567890abcdef`
    }
  );

  assert.equal(merged.Deepseek__ApiKey, key);
  assert.equal(merged.LocalLlm__Enabled, "false");
  assert.equal(merged.LocalLlm__ApiKey, undefined);
});

test("convertPostgresUrlToNpgsql accepts common PostgreSQL URLs", () => {
  assert.equal(
    convertPostgresUrlToNpgsql("postgresql://postgres:p%40ss@localhost:5432/aisd_ss26_group_7?sslmode=require"),
    "Host=localhost;Port=5432;Database=aisd_ss26_group_7;Username=postgres;Password=p@ss;SSL Mode=require"
  );
});

test("mergeEffectiveConfig derives database config from DATABASE_URL", () => {
  const merged = mergeEffectiveConfig({}, {
    DATABASE_URL: "postgres://postgres:secret@localhost:5432/aisd_ss26_group_7"
  });

  assert.equal(merged.Database__Provider, "PostgreSql");
  assert.equal(
    merged.ConnectionStrings__DefaultConnection,
    "Host=localhost;Port=5432;Database=aisd_ss26_group_7;Username=postgres;Password=secret"
  );
});

test("mergeEffectiveConfig keeps local SQLite provider for generated config", () => {
  const repoRoot = path.join(os.tmpdir(), "ojsharp repo");
  const merged = mergeEffectiveConfig(buildLocalDatabaseConfig(repoRoot), {});

  assert.equal(merged.Database__Provider, "Sqlite");
  assert.equal(isSqliteConnectionString(merged.ConnectionStrings__DefaultConnection), true);
});

test("parseDotnetUserSecrets reads colon-delimited config keys", () => {
  const parsed = parseDotnetUserSecrets([
    "Deepseek:ApiKey = test-key",
    "ConnectionStrings:DefaultConnection = Host=localhost;Database=app"
  ].join("\n"));

  assert.equal(parsed["Deepseek:ApiKey"], "test-key");
  assert.equal(parsed["ConnectionStrings:DefaultConnection"], "Host=localhost;Database=app");
});

test("diagnoseBackendFailure gives external database repair guidance", () => {
  const guidance = diagnoseBackendFailure(
    "Npgsql.PostgresException: 3D000: database \"aisd_ss26_group_7\" does not exist",
    { ConnectionStrings__DefaultConnection: "Host=localhost;Database=aisd_ss26_group_7;Username=postgres" }
  ).join("\n");

  assert.match(guidance, /createdb -U postgres aisd_ss26_group_7/);
});

test("buildLocalSqliteConnectionString uses the repository-owned local database file", () => {
  const repoRoot = path.join(os.tmpdir(), "ojsharp repo");
  assert.equal(
    buildLocalSqliteConnectionString(repoRoot),
    `Data Source=${path.join(repoRoot, ".local-data", "ojsharp-dev.sqlite")}`
  );
});

test("buildLocalDatabaseConfig selects SQLite without external input", () => {
  const repoRoot = path.join(os.tmpdir(), "ojsharp repo");
  const config = buildLocalDatabaseConfig(repoRoot);

  assert.equal(config.Database__Provider, "Sqlite");
  assert.equal(isSqliteConnectionString(config.ConnectionStrings__DefaultConnection), true);
});

test("buildBackendRunArgs supports seed-only backend startup", () => {
  assert.deepEqual(
    buildBackendRunArgs(["--seed-admin-only"]),
    ["run", "--project", path.join("Backend", "Backend", "Backend.csproj"), "--", "--seed-admin-only"]
  );
});

test("selectPathCommandCandidate prefers executable Windows npm shim", () => {
  assert.equal(
    selectPathCommandCandidate("npm", [
      "C:\\Program Files\\nodejs\\npm",
      "C:\\Program Files\\nodejs\\npm.cmd"
    ], "win32"),
    "C:\\Program Files\\nodejs\\npm.cmd"
  );

  assert.equal(
    selectPathCommandCandidate("npm", [
      "C:\\Program Files\\nodejs\\npm"
    ], "win32", (candidate) => candidate === "C:\\Program Files\\nodejs\\npm.cmd"),
    "C:\\Program Files\\nodejs\\npm.cmd"
  );
});

test("resolveNpmCliPath avoids shell quoting issues for Windows npm.cmd", () => {
  const npmPath = "C:\\Program Files\\nodejs\\npm.cmd";
  const npmCliPath = "C:\\Program Files\\nodejs\\node_modules\\npm\\bin\\npm-cli.js";

  assert.equal(
    resolveNpmCliPath(npmPath, (candidate) => candidate === npmCliPath, "win32"),
    npmCliPath
  );
});

test("resolveBackendHealthUrl uses the first ASP.NET URL", () => {
  assert.equal(
    resolveBackendHealthUrl("http://localhost:5140;http://localhost:5141"),
    "http://localhost:5140/api/v1/health"
  );
});

test("resolveBackendPort uses the first ASP.NET URL", () => {
  assert.equal(resolveBackendPort("http://localhost:5140;http://localhost:5141"), 5140);
  assert.equal(resolveBackendPort("https://example.test"), 443);
});

test("resolveUrlPort uses explicit or protocol default ports", () => {
  assert.equal(resolveUrlPort("http://localhost:3000"), 3000);
  assert.equal(resolveUrlPort("http://localhost"), 80);
});

test("isSafeBackendProcessCommand recognizes Backend listeners only", () => {
  assert.equal(isSafeBackendProcessCommand("/repo/Backend/Backend/bin/Debug/net9.0/Backend"), true);
  assert.equal(isSafeBackendProcessCommand("C:\\repo\\Backend\\bin\\Debug\\Backend.exe"), true);
  assert.equal(isSafeBackendProcessCommand("/usr/local/bin/dotnet /repo/Backend/Backend.dll"), true);
  assert.equal(isSafeBackendProcessCommand('"dotnet" exec "C:\\repo\\Backend\\Backend\\bin\\Debug\\net9.0\\Backend.dll"'), true);
  assert.equal(isSafeBackendProcessCommand("/usr/local/bin/node server.js"), false);
});

test("describeSandboxRuntime distinguishes configured and reachable Docker hosts", () => {
  const config = { DOCKER_HOST: "npipe://./pipe/dockerDesktopLinuxEngine" };

  assert.match(describeSandboxRuntime(config, () => false), /configured but unreachable/);
  assert.match(describeSandboxRuntime(config, () => true), /detected/);
});

test("isSafeFrontendProcessCommand recognizes local Next.js listeners only", () => {
  assert.equal(isSafeFrontendProcessCommand("next-server (v16.2.7)"), true);
  assert.equal(isSafeFrontendProcessCommand("/repo/node_modules/.bin/next dev"), true);
  assert.equal(isSafeFrontendProcessCommand("/usr/local/bin/node /repo/node_modules/.bin/next dev"), true);
  assert.equal(isSafeFrontendProcessCommand('"C:\\Program Files\\nodejs\\node.exe" "C:\\repo\\node_modules\\next\\dist\\server\\lib\\start-server.js"'), true);
  assert.equal(isSafeFrontendProcessCommand("/usr/local/bin/node api.js"), false);
});

test("shouldRunNpmCi skips reinstall when package-lock hash marker matches", () => {
  const repo = fs.mkdtempSync(path.join(os.tmpdir(), "ojsharp-startup-"));
  const nodeModules = path.join(repo, "node_modules");
  const packageLock = path.join(repo, "package-lock.json");
  const installedLock = path.join(nodeModules, ".package-lock.json");
  const marker = path.join(nodeModules, ".ojsharp-package-lock.sha256");

  fs.mkdirSync(nodeModules, { recursive: true });
  fs.writeFileSync(packageLock, "{\"lockfileVersion\":3}");
  fs.writeFileSync(installedLock, "{}");
  fs.writeFileSync(marker, `${sha256(fs.readFileSync(packageLock))}\n`);

  assert.equal(shouldRunNpmCi(repo), false);

  fs.writeFileSync(packageLock, "{\"lockfileVersion\":3,\"changed\":true}");

  assert.equal(shouldRunNpmCi(repo), true);
});

test("shouldRunNpmCi runs once when install marker is missing", () => {
  const repo = fs.mkdtempSync(path.join(os.tmpdir(), "ojsharp-startup-"));
  const nodeModules = path.join(repo, "node_modules");

  fs.mkdirSync(nodeModules, { recursive: true });
  fs.writeFileSync(path.join(repo, "package-lock.json"), "{\"lockfileVersion\":3}");
  fs.writeFileSync(path.join(nodeModules, ".package-lock.json"), "{}");

  assert.equal(shouldRunNpmCi(repo), true);
});

function sha256(content) {
  return createHash("sha256").update(content).digest("hex");
}
