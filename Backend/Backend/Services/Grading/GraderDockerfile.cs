namespace Backend.Services.Grading;

internal static class GraderDockerfile
{
    public const string Content = """
FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip nodejs npm \
    && python3 -m pip install --break-system-packages \
        pytest flask flask-cors requests beautifulsoup4 \
    && npm install -g jest jest-environment-jsdom typescript supertest express \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash sandbox \
    && mkdir -p /workspace \
    && chown -R sandbox:sandbox /workspace

ENV PYTHONDONTWRITEBYTECODE=1
WORKDIR /workspace
USER sandbox
CMD ["sleep", "infinity"]
""";
}
