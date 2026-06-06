import fsp from "node:fs/promises";
import path from "node:path";

export const localDatabase = {
  directoryName: ".local-data",
  fileName: "ojsharp-dev.sqlite",
  provider: "Sqlite"
};

export const localSeedAdminDefaults = {
  email: "admin@example.com",
  password: "Admin123!"
};

export async function ensureLocalDatabaseConfig(repoRoot) {
  const directory = path.join(repoRoot, localDatabase.directoryName);
  await fsp.mkdir(directory, { recursive: true });

  return buildLocalDatabaseConfig(repoRoot);
}

export function buildLocalDatabaseConfig(repoRoot) {
  return {
    Database__Provider: localDatabase.provider,
    ConnectionStrings__DefaultConnection: buildLocalSqliteConnectionString(repoRoot)
  };
}

export function buildLocalSqliteConnectionString(repoRoot) {
  return `Data Source=${path.join(repoRoot, localDatabase.directoryName, localDatabase.fileName)}`;
}

export function isSqliteConnectionString(value) {
  return /^Data Source\s*=/i.test(String(value ?? "").trim());
}
