namespace Backend.Services.Grading;

internal static class GraderDockerfile
{
    public const string Content = """
FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip nodejs npm \
    && python3 -m pip install --break-system-packages pytest \
    && npm install -g jest typescript \
    && rm -rf /var/lib/apt/lists/*

ENV PYTHONDONTWRITEBYTECODE=1
WORKDIR /workspace
CMD ["sleep", "infinity"]
""";
}
