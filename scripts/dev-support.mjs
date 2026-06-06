const connectionStringKey = "ConnectionStrings__DefaultConnection";

export function isUsableConfigValue(value) {
  if (typeof value !== "string") {
    return false;
  }

  const trimmed = value.trim();
  return trimmed.length > 0
    && !trimmed.startsWith("<")
    && !trimmed.endsWith(">")
    && !/^change-?me$/i.test(trimmed)
    && !/^todo$/i.test(trimmed);
}

export function normalizePostgresConnectionString(value) {
  const trimmed = String(value ?? "").trim();
  if (!trimmed) {
    return "";
  }

  if (/^postgres(ql)?:\/\//i.test(trimmed)) {
    return convertPostgresUrlToNpgsql(trimmed);
  }

  return trimmed;
}

export function convertPostgresUrlToNpgsql(value) {
  let url;
  try {
    url = new URL(value);
  } catch {
    return value.trim();
  }

  if (!/^postgres(ql)?:$/i.test(url.protocol)) {
    return value.trim();
  }

  const parts = {
    Host: url.hostname,
    Port: url.port || "5432",
    Database: decodeURIComponent(url.pathname.replace(/^\/+/, "")),
    Username: decodeURIComponent(url.username),
    Password: decodeURIComponent(url.password)
  };

  for (const [key, queryValue] of url.searchParams.entries()) {
    const mappedKey = mapPostgresUrlQueryKey(key);
    if (mappedKey) {
      parts[mappedKey] = queryValue;
    }
  }

  return serializeNpgsqlConnectionString(parts);
}

export function serializeNpgsqlConnectionString(parts) {
  return Object.entries(parts)
    .filter(([, value]) => isUsableConfigValue(String(value ?? "")))
    .map(([key, value]) => `${key}=${escapeNpgsqlConnectionValue(String(value))}`)
    .join(";");
}

function escapeNpgsqlConnectionValue(value) {
  if (!/[;"'=\s]/.test(value)) {
    return value;
  }

  return `"${value.replaceAll("\\", "\\\\").replaceAll("\"", "\\\"")}"`;
}

function mapPostgresUrlQueryKey(key) {
  const normalized = key.toLowerCase().replaceAll("_", "");
  if (normalized === "sslmode") {
    return "SSL Mode";
  }

  if (normalized === "trustservercertificate") {
    return "Trust Server Certificate";
  }

  if (normalized === "applicationname") {
    return "Application Name";
  }

  return "";
}

export function derivePostgresConnectionFromEnvironment(env = process.env) {
  if (isUsableConfigValue(env.DATABASE_URL)) {
    return normalizePostgresConnectionString(env.DATABASE_URL);
  }

  if (!isUsableConfigValue(env.PGHOST)
      || !isUsableConfigValue(env.PGDATABASE)
      || !isUsableConfigValue(env.PGUSER)) {
    return "";
  }

  return serializeNpgsqlConnectionString({
    Host: env.PGHOST,
    Port: env.PGPORT || "5432",
    Database: env.PGDATABASE,
    Username: env.PGUSER,
    Password: env.PGPASSWORD || ""
  });
}

export function parseDotnetUserSecrets(content) {
  const result = {};
  for (const rawLine of content.split(/\r?\n/)) {
    const separatorIndex = rawLine.indexOf(" = ");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = rawLine.slice(0, separatorIndex).trim();
    const value = rawLine.slice(separatorIndex + 3).trim();
    if (key && value) {
      result[key] = value;
    }
  }

  return result;
}

export function diagnoseBackendFailure(logText, config = {}) {
  const text = String(logText ?? "");
  const lower = text.toLowerCase();
  const guidance = ["Startup repair guide:"];

  if (!text.trim()) {
    guidance.push("- The backend produced no error log. Check backend-dev.err.log and backend-dev.log for process output.");
    return guidance;
  }

  if (lower.includes("connectionstrings__defaultconnection must be configured")) {
    guidance.push("- Database config is missing. Rerun npm run dev so the local SQLite database file can be configured automatically.");
  }

  if (lower.includes("seed admin email") || lower.includes("seed admin password")) {
    guidance.push("- Seed admin config is missing or invalid. Rerun npm run dev and enter a real admin email plus an 8+ character password.");
  }

  if (lower.includes("password authentication failed")) {
    guidance.push("- PostgreSQL rejected the password. Local npm run dev no longer needs PostgreSQL; rerun npm run dev to use the SQLite file database.");
    guidance.push("  If this is an external database, repair the remote credentials and update .env.local.");
  }

  if (lower.includes("role") && lower.includes("does not exist")) {
    guidance.push("- The PostgreSQL user/role in the connection string does not exist. Local npm run dev no longer needs PostgreSQL; external databases still need a valid role.");
  }

  if (lower.includes("database") && lower.includes("does not exist")) {
    const databaseName = extractNpgsqlPart(config[connectionStringKey], "Database") || "aisd_ss26_group_7";
    guidance.push("- The database does not exist. Local npm run dev creates a SQLite file automatically; external databases must be repaired manually.");
    guidance.push(`  External database repair example: createdb -U postgres ${databaseName}`);
    guidance.push(`  PowerShell/psql alternative: psql -U postgres -c "CREATE DATABASE ${databaseName};"`);
  }

  if (lower.includes("permission denied")
      || lower.includes("must be owner")
      || lower.includes("insufficient privilege")) {
    const databaseName = extractNpgsqlPart(config[connectionStringKey], "Database") || "aisd_ss26_group_7";
    const userName = extractNpgsqlPart(config[connectionStringKey], "Username")
      || extractNpgsqlPart(config[connectionStringKey], "User Id")
      || "YOUR_USER";
    guidance.push("- PostgreSQL permissions are not enough for schema creation or seed repair. Local npm run dev no longer needs PostgreSQL permissions.");
    guidance.push(`  Ask the DB owner to grant privileges, or run as an owner/admin: GRANT ALL PRIVILEGES ON DATABASE ${databaseName} TO ${userName};`);
  }

  if (lower.includes("connection refused")
      || lower.includes("actively refused")
      || lower.includes("could not connect")
      || lower.includes("no route to host")) {
    guidance.push("- PostgreSQL is unreachable. Local npm run dev no longer needs PostgreSQL; rerun npm run dev to regenerate the SQLite local config.");
    guidance.push("  If this is an external database, verify host/port and confirm local firewalls/VPN are not blocking the configured port.");
  }

  if (lower.includes("cannot connect to the docker daemon")
      || lower.includes("docker daemon")
      || (lower.includes("permission denied") && lower.includes("docker"))) {
    guidance.push("- Docker is unavailable or not authorized. The app can still start; Docker is only needed for sandbox run/submit verification.");
  }

  if (guidance.length === 1) {
    guidance.push("- Read the backend log above. Rerun npm run dev to regenerate local SQLite config, or repair explicitly configured external database settings.");
  }

  return guidance;
}

function extractNpgsqlPart(connectionString, key) {
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
