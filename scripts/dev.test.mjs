import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  buildLocalPostgresConnectionString,
  convertPostgresUrlToNpgsql,
  diagnoseBackendFailure,
  isDatabaseStartupFailure,
  isDockerCredentialHelperFailure,
  isLocalDatabaseTarget,
  isUsableConfigValue,
  mergeEffectiveConfig,
  parseEnvFileContent,
  parseDotnetUserSecrets,
  parseDockerPortOutput,
  resolveBackendHealthUrl,
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
  const merged = mergeEffectiveConfig(
    { BackendUrls: "http://localhost:5140", Deepseek__ApiKey: "local-key" },
    { BackendUrls: "http://127.0.0.1:6000" }
  );

  assert.equal(merged.BackendUrls, "http://127.0.0.1:6000");
  assert.equal(merged.Deepseek__ApiKey, "local-key");
  assert.equal(merged.NEXT_PUBLIC_API_BASE_URL, "http://localhost:5140/api/v1");
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

  assert.equal(
    merged.ConnectionStrings__DefaultConnection,
    "Host=localhost;Port=5432;Database=aisd_ss26_group_7;Username=postgres;Password=secret"
  );
});

test("parseDotnetUserSecrets reads colon-delimited config keys", () => {
  const parsed = parseDotnetUserSecrets([
    "Deepseek:ApiKey = test-key",
    "ConnectionStrings:DefaultConnection = Host=localhost;Database=app"
  ].join("\n"));

  assert.equal(parsed["Deepseek:ApiKey"], "test-key");
  assert.equal(parsed["ConnectionStrings:DefaultConnection"], "Host=localhost;Database=app");
});

test("diagnoseBackendFailure gives database repair guidance", () => {
  const guidance = diagnoseBackendFailure(
    "Npgsql.PostgresException: 3D000: database \"aisd_ss26_group_7\" does not exist",
    { ConnectionStrings__DefaultConnection: "Host=localhost;Database=aisd_ss26_group_7;Username=postgres" }
  ).join("\n");

  assert.match(guidance, /createdb -U postgres aisd_ss26_group_7/);
});

test("buildLocalPostgresConnectionString uses the project-owned demo database", () => {
  assert.equal(
    buildLocalPostgresConnectionString(55432),
    "Host=127.0.0.1;Port=55432;Database=aisd_ss26_group_7;Username=postgres;Password=postgres"
  );
});

test("parseDockerPortOutput reads the published host port", () => {
  assert.equal(parseDockerPortOutput("127.0.0.1:55432"), 55432);
  assert.equal(parseDockerPortOutput("0.0.0.0:55433\n:::55433"), 55433);
});

test("isDockerCredentialHelperFailure recognizes missing Docker helper output", () => {
  assert.equal(
    isDockerCredentialHelperFailure("docker: error getting credentials - err: exec: \"docker-credential-desktop\": executable file not found in $PATH"),
    true
  );
  assert.equal(isDockerCredentialHelperFailure("docker: Cannot connect to the Docker daemon"), false);
});

test("isDatabaseStartupFailure recognizes local PostgreSQL repair signals", () => {
  assert.equal(isDatabaseStartupFailure("Npgsql.PostgresException: 28P01: password authentication failed for user postgres"), true);
  assert.equal(isDatabaseStartupFailure("Connection refused 127.0.0.1:5432"), true);
  assert.equal(isDatabaseStartupFailure("The frontend failed to compile"), false);
});

test("isLocalDatabaseTarget allows only local PostgreSQL targets for automatic repair", () => {
  assert.equal(isLocalDatabaseTarget("Host=localhost;Database=aisd_ss26_group_7"), true);
  assert.equal(isLocalDatabaseTarget("Host=127.0.0.1;Database=aisd_ss26_group_7"), true);
  assert.equal(isLocalDatabaseTarget("Host=db.example.com;Database=aisd_ss26_group_7"), false);
});

test("resolveBackendHealthUrl uses the first ASP.NET URL", () => {
  assert.equal(
    resolveBackendHealthUrl("http://localhost:5140;http://localhost:5141"),
    "http://localhost:5140/api/v1/health"
  );
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
