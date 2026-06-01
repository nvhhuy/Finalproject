# Build context: repository root
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Backend/ src/Backend/
RUN dotnet restore src/Backend/Worker/Worker.csproj
RUN dotnet publish src/Backend/Worker/Worker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# ffmpeg/ffprobe for media transcode when VideoProcessing paths point at these binaries.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl \
    && rm -rf /var/lib/apt/lists/* \
    && addgroup --system --gid 1001 appgroup \
    && adduser --system --uid 1001 --ingroup appgroup appuser
ENV VideoProcessing__FfmpegPath=/usr/bin/ffmpeg \
    VideoProcessing__FfprobePath=/usr/bin/ffprobe
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser
# Worker has no HTTP port; check that the process is alive via its pid file.
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD pgrep -x dotnet >/dev/null || exit 1
ENTRYPOINT ["dotnet", "Worker.dll"]
