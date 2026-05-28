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
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
ENV VideoProcessing__FfmpegPath=/usr/bin/ffmpeg \
    VideoProcessing__FfprobePath=/usr/bin/ffprobe
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Worker.dll"]
