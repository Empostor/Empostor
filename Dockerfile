# ============================================
# Empostor Server Docker Image
# ============================================
# Multi-stage build: compiles from source, then produces a minimal runtime image.
#
# Build (local):
#   docker build -t empostor-server .
#
# Build (multi-arch):
#   docker buildx build --platform linux/amd64,linux/arm64 -t empostor-server .
#
# Run:
#   docker compose up -d
# ============================================

# ── Build stage ────────────────────────────
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG TARGETARCH
ARG VERSIONSUFFIX=docker

WORKDIR /source

# Restore dependencies (layer caching)
COPY src/Empostor.Server/Empostor.Server.csproj ./src/Empostor.Server/
COPY src/Empostor.Api/Empostor.Api.csproj ./src/Empostor.Api/
COPY src/Empostor.Api.Innersloth.Generator/Empostor.Api.Innersloth.Generator.csproj ./src/Empostor.Api.Innersloth.Generator/
COPY src/NextFast.Hazel-New/Next.Hazel/Next.Hazel.csproj ./src/NextFast.Hazel-New/Next.Hazel/
COPY src/NextFast.Hazel-New/Next.Hazel.Abstractions/Next.Hazel.Abstractions.csproj ./src/NextFast.Hazel-New/Next.Hazel.Abstractions/
COPY src/Directory.Build.props ./src/

RUN case "$TARGETARCH" in \
    amd64) RID=linux-x64 ;; \
    arm64) RID=linux-arm64 ;; \
    arm)   RID=linux-arm ;; \
    *)     echo "Unsupported arch: $TARGETARCH"; exit 1 ;; \
  esac && \
  dotnet restore -r "$RID" ./src/Empostor.Server/Empostor.Server.csproj

# Copy sources and publish
COPY src/. ./src/

RUN case "$TARGETARCH" in \
    amd64) RID=linux-x64 ;; \
    arm64) RID=linux-arm64 ;; \
    arm)   RID=linux-arm ;; \
    *)     echo "Unsupported arch: $TARGETARCH"; exit 1 ;; \
  esac && \
  [ "$VERSIONSUFFIX" = "none" ] && VERSIONSUFFIX="" ; \
  dotnet publish ./src/Empostor.Server/Empostor.Server.csproj \
    -c Release \
    -r "$RID" \
    --no-restore \
    --self-contained false \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:VersionSuffix="$VERSIONSUFFIX" \
    -o /app

# Copy default config (must be overridden in production)
RUN cp ./src/Empostor.Server/config.json /app/config.json 2>/dev/null || true

# ── Runtime stage ──────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published output from build stage
COPY --from=build /app ./

# Create writable runtime directories
RUN mkdir -p /app/plugins /app/libraries /app/Languages /app/Log /app/Data /app/marketplace \
  && chmod 777 /app/plugins /app/libraries /app/Languages /app/Log /app/Data /app/marketplace

# Environment
ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV EMPOSTOR_Server__PublicIp=0.0.0.0
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

EXPOSE 80/tcp
EXPOSE 22023/tcp
EXPOSE 22023/udp

VOLUME ["/app/config", "/app/plugins", "/app/libraries", "/app/Languages", "/app/Log", "/app/Data", "/app/marketplace"]

ENTRYPOINT ["./Empostor.Server"]
