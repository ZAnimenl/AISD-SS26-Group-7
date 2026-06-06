import { spawnSync } from "node:child_process";
import process from "node:process";

export function resolveUrlPort(value) {
  const url = new URL(value);
  if (url.port) {
    return Number.parseInt(url.port, 10);
  }

  return url.protocol === "https:" ? 443 : 80;
}

export function isSafeBackendProcessCommand(command) {
  const normalized = String(command ?? "").trim();
  return /(?:^|[\\/])Backend(?:\.exe)?(?:\s|$)/i.test(normalized)
    || /(?:^|[\\/])dotnet(?:\.exe)?(?:\s|$).*Backend/i.test(normalized);
}

export function isSafeFrontendProcessCommand(command) {
  const normalized = String(command ?? "").trim();
  return /next-server/i.test(normalized)
    || /(?:^|[\\/])next(?:\.cmd)?(?:\s|$).*dev/i.test(normalized)
    || /(?:^|[\\/])node(?:\.exe)?(?:\s|$).*next.*dev/i.test(normalized);
}

export function findListeningProcessIds(port, cwd = process.cwd()) {
  if (process.platform === "win32") {
    return findWindowsListeningProcessIds(port, cwd);
  }

  const result = spawnSync("lsof", ["-nP", `-iTCP:${port}`, "-sTCP:LISTEN", "-t"], {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  if (result.status !== 0) {
    return [];
  }

  return uniqueProcessIds(result.stdout);
}

export function readProcessCommand(processId, cwd = process.cwd()) {
  if (process.platform === "win32") {
    const result = spawnSync("tasklist", ["/FI", `PID eq ${processId}`, "/FO", "CSV", "/NH"], {
      cwd,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"]
    });
    return result.status === 0 ? result.stdout : "";
  }

  const result = spawnSync("ps", ["-p", String(processId), "-o", "command="], {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });
  return result.status === 0 ? result.stdout : "";
}

export function stopProcessIds(processIds, cwd = process.cwd()) {
  for (const processId of processIds) {
    killProcess(processId, cwd);
  }
}

function findWindowsListeningProcessIds(port, cwd) {
  const result = spawnSync("netstat", ["-ano", "-p", "tcp"], {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });

  if (result.status !== 0) {
    return [];
  }

  const portPattern = new RegExp(`:${port}\\s+.*\\s+LISTENING\\s+(\\d+)`, "i");
  const ids = [];
  for (const line of result.stdout.split(/\r?\n/)) {
    const match = line.match(portPattern);
    if (match) {
      ids.push(match[1]);
    }
  }

  return uniqueProcessIds(ids.join("\n"));
}

function uniqueProcessIds(output) {
  return [...new Set(String(output)
    .split(/\s+/)
    .map((value) => Number.parseInt(value, 10))
    .filter((value) => Number.isInteger(value) && value > 0))];
}

function killProcess(processId, cwd) {
  const result = process.platform === "win32"
    ? spawnSync("taskkill", ["/PID", String(processId), "/T", "/F"], {
        cwd,
        stdio: "inherit"
      })
    : spawnSync("kill", [String(processId)], {
        cwd,
        stdio: "inherit"
      });

  if (result.status !== 0) {
    throw new Error([
      `Could not stop the old local process ${processId}.`,
      "Close it manually, approve any OS permission prompt, then rerun npm run dev."
    ].join("\n"));
  }
}
