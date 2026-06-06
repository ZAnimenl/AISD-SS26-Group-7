import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

export const localPostgres = {
  containerName: "ojsharp-postgres-dev",
  database: "aisd_ss26_group_7",
  image: "postgres:16-alpine",
  password: "postgres",
  portEnd: 55449,
  portStart: 55432,
  user: "postgres",
  volumeName: "ojsharp-postgres-dev-data"
};

export const localSeedAdminDefaults = {
  email: "admin@example.com",
  password: "Admin123!"
};

export async function tryProvisionLocalPostgres({ docker, repoRoot, runCommand, delay }) {
  if (!docker || getDockerDaemonStatus(docker, repoRoot).startsWith("not reachable")) {
    return {};
  }

  try {
    const port = await ensureLocalPostgresContainer({ docker, repoRoot, runCommand, delay });
    return {
      ConnectionStrings__DefaultConnection: buildLocalPostgresConnectionString(port)
    };
  } catch (exception) {
    console.log("Local Docker PostgreSQL could not be prepared automatically.");
    console.log(exception.message);
    return {};
  }
}

export function buildLocalPostgresConnectionString(port = localPostgres.portStart) {
  return [
    "Host=127.0.0.1",
    `Port=${port}`,
    `Database=${localPostgres.database}`,
    `Username=${localPostgres.user}`,
    `Password=${localPostgres.password}`
  ].join(";");
}

export function getDockerDaemonStatus(docker, repoRoot) {
  if (!docker) {
    return "cannot check until Docker CLI is installed";
  }

  const result = spawnSync(docker, ["version", "--format", "{{.Server.Version}}"], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  });

  if (result.status === 0 && result.stdout.trim()) {
    return `running (${result.stdout.trim()})`;
  }

  return "not reachable; start Docker Desktop/Colima and approve any OS permission prompt";
}

async function ensureLocalPostgresContainer({ docker, repoRoot, runCommand, delay }) {
  const existingId = getLocalPostgresContainerId(docker, repoRoot);
  if (existingId) {
    await startLocalPostgresContainer({ docker, repoRoot, runCommand });
    const port = getLocalPostgresPort(docker, repoRoot) || localPostgres.portStart;
    if (await waitForLocalPostgres({ docker, repoRoot, delay })) {
      console.log(`Local PostgreSQL is ready in Docker container ${localPostgres.containerName}.`);
      return port;
    }

    console.log(`Resetting project-owned PostgreSQL container ${localPostgres.containerName}.`);
    resetLocalPostgresStorage(docker, repoRoot);
  }

  const port = createLocalPostgresContainer(docker, repoRoot);
  if (!await waitForLocalPostgres({ docker, repoRoot, delay })) {
    throw new Error("Docker PostgreSQL started but did not become ready. Check Docker Desktop/Colima permissions and rerun npm run dev.");
  }

  console.log(`Local PostgreSQL is ready in Docker container ${localPostgres.containerName}.`);
  return port;
}

function getLocalPostgresContainerId(docker, repoRoot) {
  const result = spawnSync(docker, [
    "ps",
    "-a",
    "--filter",
    `name=^/${localPostgres.containerName}$`,
    "--format",
    "{{.ID}}"
  ], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  return result.status === 0 ? result.stdout.trim() : "";
}

async function startLocalPostgresContainer({ docker, repoRoot, runCommand }) {
  const running = spawnSync(docker, [
    "inspect",
    "-f",
    "{{.State.Running}}",
    localPostgres.containerName
  ], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  if (running.status === 0 && running.stdout.trim() === "true") {
    return;
  }

  await runCommand(docker, ["start", localPostgres.containerName], { stdio: "ignore" });
}

function createLocalPostgresContainer(docker, repoRoot) {
  for (let port = localPostgres.portStart; port <= localPostgres.portEnd; port += 1) {
    const dockerRunArgs = [
      "run",
      "-d",
      "--name",
      localPostgres.containerName,
      "-e",
      `POSTGRES_DB=${localPostgres.database}`,
      "-e",
      `POSTGRES_USER=${localPostgres.user}`,
      "-e",
      `POSTGRES_PASSWORD=${localPostgres.password}`,
      "-p",
      `127.0.0.1:${port}:5432`,
      "-v",
      `${localPostgres.volumeName}:/var/lib/postgresql/data`,
      localPostgres.image
    ];
    const result = spawnSync(docker, dockerRunArgs, {
      cwd: repoRoot,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    });
    const finalResult = isDockerCredentialHelperFailure(result.stderr)
      ? retryDockerRunWithPublicConfig(docker, repoRoot, dockerRunArgs)
      : result;

    if (finalResult.status === 0) {
      return port;
    }

    if (!/port is already allocated|bind/i.test(finalResult.stderr)) {
      throw new Error(finalResult.stderr.trim() || `docker run failed with exit code ${finalResult.status}.`);
    }
  }

  throw new Error(`No free localhost port found from ${localPostgres.portStart} to ${localPostgres.portEnd}.`);
}

function getLocalPostgresPort(docker, repoRoot) {
  const result = spawnSync(docker, [
    "port",
    localPostgres.containerName,
    "5432/tcp"
  ], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  if (result.status !== 0) {
    return 0;
  }

  return parseDockerPortOutput(result.stdout);
}

export function parseDockerPortOutput(value) {
  const match = String(value ?? "").match(/:(\d+)\s*$/m);
  return match ? Number.parseInt(match[1], 10) : 0;
}

export function isDockerCredentialHelperFailure(value) {
  const text = String(value ?? "").toLowerCase();
  return text.includes("error getting credentials")
    && text.includes("docker-credential-")
    && (text.includes("not found") || text.includes("executable file not found"));
}

export function isDatabaseStartupFailure(value) {
  const text = String(value ?? "").toLowerCase();
  return text.includes("connectionstrings__defaultconnection must be configured")
    || text.includes("password authentication failed")
    || (text.includes("role") && text.includes("does not exist"))
    || (text.includes("database") && text.includes("does not exist"))
    || text.includes("insufficient privilege")
    || text.includes("must be owner")
    || text.includes("connection refused")
    || text.includes("actively refused")
    || text.includes("could not connect")
    || text.includes("no route to host");
}

export function isLocalDatabaseTarget(connectionString) {
  const host = extractConnectionPart(connectionString, "Host")
    || extractConnectionPart(connectionString, "Server")
    || extractConnectionPart(connectionString, "Data Source");
  if (!host) {
    return true;
  }

  return [
    ".",
    "(local)",
    "::1",
    "127.0.0.1",
    "host.docker.internal",
    "localhost"
  ].includes(host.trim().toLowerCase());
}

async function waitForLocalPostgres({ docker, repoRoot, delay }) {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const result = spawnSync(docker, [
      "exec",
      "-e",
      `PGPASSWORD=${localPostgres.password}`,
      localPostgres.containerName,
      "psql",
      "-U",
      localPostgres.user,
      "-d",
      localPostgres.database,
      "-c",
      "select 1"
    ], {
      cwd: repoRoot,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    });

    if (result.status === 0) {
      return true;
    }

    await delay(1000);
  }

  return false;
}

function resetLocalPostgresStorage(docker, repoRoot) {
  spawnSync(docker, ["rm", "-f", localPostgres.containerName], {
    cwd: repoRoot,
    stdio: "ignore"
  });
  spawnSync(docker, ["volume", "rm", "-f", localPostgres.volumeName], {
    cwd: repoRoot,
    stdio: "ignore"
  });
}

function retryDockerRunWithPublicConfig(docker, repoRoot, args) {
  console.log("Docker credential helper is unavailable; retrying public PostgreSQL image pull with a local no-credential Docker config.");
  return spawnSync(docker, args, {
    cwd: repoRoot,
    encoding: "utf8",
    env: buildPublicDockerConfigEnv(docker, repoRoot),
    stdio: ["ignore", "pipe", "pipe"]
  });
}

function buildPublicDockerConfigEnv(docker, repoRoot) {
  const configDir = path.join(os.tmpdir(), "ojsharp-docker-public-config");
  fs.mkdirSync(configDir, { recursive: true });
  fs.writeFileSync(path.join(configDir, "config.json"), "{\"auths\":{}}\n", { mode: 0o600 });

  const env = { ...process.env, DOCKER_CONFIG: configDir };
  if (!env.DOCKER_HOST) {
    const dockerHost = readCurrentDockerHost(docker, repoRoot);
    if (dockerHost) {
      env.DOCKER_HOST = dockerHost;
    }
  }

  return env;
}

function readCurrentDockerHost(docker, repoRoot) {
  const result = spawnSync(docker, ["context", "inspect", "--format", "{{json .Endpoints.docker.Host}}"], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  if (result.status !== 0 || !result.stdout.trim()) {
    return "";
  }

  try {
    return JSON.parse(result.stdout.trim());
  } catch {
    return result.stdout.trim();
  }
}

function extractConnectionPart(connectionString, key) {
  const pattern = new RegExp(`(?:^|;)\\s*${escapeRegExp(key)}\\s*=\\s*(\"(?:\\\\.|[^\"])*\"|[^;]*)`, "i");
  const match = String(connectionString ?? "").match(pattern);
  if (!match) {
    return "";
  }

  return match[1].trim().replace(/^"|"$/g, "").replaceAll("\\\"", "\"").replaceAll("\\\\", "\\");
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
