namespace Backend.Services.Grading;

internal static class GraderDockerfile
{
    public const string Content = """
FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip nodejs npm \
    && python3 -m pip install --break-system-packages \
        pytest flask flask-cors requests beautifulsoup4 \
        fastapi "uvicorn[standard]" peewee "pydantic>=2.9" "httpx>=0.27" \
    && npm install -g jest jest-environment-jsdom jsdom@22.1.0 typescript supertest express \
        fake-indexeddb@6.2.5 jest-fetch-mock@4.2.0 \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash sandbox \
    && mkdir -p /workspace \
    && chown -R sandbox:sandbox /workspace

ENV PYTHONDONTWRITEBYTECODE=1
ENV NODE_PATH=/usr/local/lib/node_modules
WORKDIR /workspace
USER sandbox
CMD ["sleep", "infinity"]
""";
}
