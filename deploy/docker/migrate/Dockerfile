# One-shot migration image. Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY src/Backend/ src/Backend/
RUN dotnet tool install --global dotnet-ef --version 8.0.11
ENV PATH="/root/.dotnet/tools:${PATH}"
ENTRYPOINT ["dotnet", "ef", "database", "update", \
    "--project", "src/Backend/Infrastructure/Infrastructure.csproj", \
    "--startup-project", "src/Backend/Api/Api.csproj", \
    "--context", "ApplicationDbContext"]
