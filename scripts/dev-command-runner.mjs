import { spawn, spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";

export function createCommandRunner() {
  const resolvedCommandSpecs = new Map();

  function ensureCommand(command, guidance) {
    const resolved = resolveCommand(command);

    if (!resolved) {
      throw new Error(`${command} is required but was not found.\n${guidance}`);
    }
  }

  function resolveCommand(command) {
    return resolveCommandSpec(command).display;
  }

  function resolveCommandSpec(command) {
    if (resolvedCommandSpecs.has(command)) {
      return resolvedCommandSpecs.get(command);
    }

    const resolved = command === "npm"
      ? resolveNpmCommandSpec()
      : {
          file: command === "dotnet" ? resolveDotnetCommand() : resolvePathCommand(command),
          argsPrefix: [],
          shell: false
        };
    const spec = {
      ...resolved,
      display: resolved.argsPrefix.length > 0
        ? `${resolved.file} ${resolved.argsPrefix.join(" ")}`
        : resolved.file
    };
    resolvedCommandSpecs.set(command, spec);
    return spec;
  }

  function runCommand(command, args, options = {}) {
    return new Promise((resolve, reject) => {
      const child = spawnCommand(command, args, options);
      child.on("error", (exception) => {
        reject(new Error(`${command} ${args.join(" ")} could not start: ${exception.message}`));
      });
      child.on("exit", (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`${command} ${args.join(" ")} failed with exit code ${code}.`));
        }
      });
    });
  }

  function spawnCommand(command, args, options = {}) {
    const spec = resolveCommandSpec(command);
    if (!spec.file) {
      throw new Error(`${command} is required but was not found.`);
    }

    return spawn(spec.file, [...spec.argsPrefix, ...args], {
      ...options,
      shell: options.shell ?? spec.shell
    });
  }

  function buildChildEnv(config = {}, dotnetCommand = resolveCommand("dotnet")) {
    const env = { ...process.env, ...config };
    const dotnetRoot = resolveDotnetRoot(dotnetCommand);
    if (dotnetRoot && !env.DOTNET_ROOT) {
      env.DOTNET_ROOT = dotnetRoot;
    }

    return env;
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

  return {
    buildChildEnv,
    ensureCommand,
    resolveCommand,
    runCommand,
    spawnCommand
  };
}

export function resolvePathCommand(command) {
  const checker = process.platform === "win32"
    ? spawnSync("where", [command], { encoding: "utf8" })
    : spawnSync("sh", ["-c", `command -v ${command}`], { encoding: "utf8" });

  if (checker.status !== 0) {
    return "";
  }

  return selectPathCommandCandidate(command, checker.stdout.trim().split(/\r?\n/)) || command;
}

export function selectPathCommandCandidate(command, candidates, platform = process.platform, exists = fs.existsSync) {
  const normalizedCandidates = candidates
    .map((candidate) => String(candidate ?? "").trim())
    .filter(Boolean);

  if (platform !== "win32") {
    return normalizedCandidates[0] ?? "";
  }

  const commandName = command.toLowerCase();
  const preferredExtensions = [".cmd", ".exe", ".bat"];
  const preferredCandidate = normalizedCandidates.find((candidate) => {
    const baseName = getPathBaseName(candidate).toLowerCase();
    return preferredExtensions.some((extension) => baseName === `${commandName}${extension}`);
  });
  if (preferredCandidate) {
    return preferredCandidate;
  }

  for (const candidate of normalizedCandidates) {
    if (!/\.[A-Za-z0-9]+$/.test(getPathBaseName(candidate))) {
      const commandShim = `${candidate}.cmd`;
      if (exists(commandShim)) {
        return commandShim;
      }
    }
  }

  return normalizedCandidates[0] ?? "";
}

function resolveNpmCommandSpec() {
  const npmExecPath = process.env.npm_execpath;
  if (npmExecPath && fs.existsSync(npmExecPath)) {
    return {
      file: process.execPath,
      argsPrefix: [npmExecPath],
      shell: false
    };
  }

  const npmPath = resolvePathCommand("npm");
  return {
    file: npmPath,
    argsPrefix: [],
    shell: needsCommandShell(npmPath)
  };
}

function needsCommandShell(commandPath, platform = process.platform) {
  return platform === "win32" && /\.(cmd|bat)$/i.test(commandPath);
}

function resolveDotnetRoot(dotnetCommand) {
  if (!dotnetCommand || dotnetCommand === "dotnet" || !path.isAbsolute(dotnetCommand)) {
    return "";
  }

  const directory = path.dirname(dotnetCommand);
  return path.basename(directory) === "libexec" ? directory : "";
}

function getPathBaseName(value) {
  return String(value).split(/[\\/]/).pop() ?? "";
}
