const legacyLocalLlmPrefix = "LocalLlm__";

export function normalizeDeepseekApiKey(value) {
  const compact = String(value ?? "").trim().replace(/\s+/g, "");
  if (!compact) {
    return "";
  }

  const markerPositions = [...compact.matchAll(/sk-/gi)].map((match) => match.index ?? -1);
  if (markerPositions.length <= 1) {
    return compact;
  }

  const fragments = markerPositions
    .map((position, index) => compact.slice(position, markerPositions[index + 1] ?? compact.length))
    .filter(Boolean);
  const firstFragment = fragments[0] ?? "";

  return fragments.length > 1 && fragments.every((fragment) => fragment === firstFragment)
    ? firstFragment
    : compact;
}

export function isAcceptableDeepseekApiKey(value) {
  const normalized = normalizeDeepseekApiKey(value);
  return /^sk-[A-Za-z0-9_-]{16,160}$/.test(normalized)
    && (normalized.match(/sk-/gi)?.length ?? 0) === 1;
}

export function cleanLocalAiConfig(config) {
  disableLegacyLocalLlm(config);

  if (Object.prototype.hasOwnProperty.call(config, "Deepseek__ApiKey")) {
    const normalizedKey = normalizeDeepseekApiKey(config.Deepseek__ApiKey);
    if (isAcceptableDeepseekApiKey(normalizedKey)) {
      config.Deepseek__ApiKey = normalizedKey;
    } else {
      delete config.Deepseek__ApiKey;
    }
  }

  return config;
}

export function normalizeEffectiveAiConfig(merged, fileConfig, processConfig) {
  disableLegacyLocalLlm(merged);

  const normalizedKey = [
    processConfig.Deepseek__ApiKey,
    fileConfig.Deepseek__ApiKey
  ]
    .map(normalizeDeepseekApiKey)
    .find(isAcceptableDeepseekApiKey);

  if (normalizedKey) {
    merged.Deepseek__ApiKey = normalizedKey;
  } else {
    delete merged.Deepseek__ApiKey;
  }

  return merged;
}

function disableLegacyLocalLlm(config) {
  for (const key of Object.keys(config)) {
    if (key.startsWith(legacyLocalLlmPrefix)) {
      delete config[key];
    }
  }

  config.LocalLlm__Enabled = "false";
  return config;
}
