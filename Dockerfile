FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH
ARG VERSION_SUFFIX=docker

WORKDIR /source
COPY src/ ./src/

RUN case "$TARGETARCH" in \
      amd64) RID=linux-x64 ;; \
      arm64) RID=linux-arm64 ;; \
      arm) RID=linux-arm ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac && \
    dotnet restore -r "$RID" ./src/Empostor.Server/Empostor.Server.csproj && \
    dotnet publish ./src/Empostor.Server/Empostor.Server.csproj \
      --configuration Release \
      --runtime "$RID" \
      --no-restore \
      --self-contained false \
      -p:PublishSingleFile=true \
      -p:PublishTrimmed=false \
      -p:VersionSuffix="$VERSION_SUFFIX" \
      --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=build /app/ ./
COPY marketplace/ ./marketplace/

RUN mkdir -p Data Log Pages Languages languages plugins libraries Update \
    && chown -R app:app Data Log Pages Languages languages plugins libraries Update \
    && chmod 0750 Data Log Pages Languages languages plugins libraries Update

ENV DOTNET_RUNNING_IN_CONTAINER=1
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

USER app
ENTRYPOINT ["./Empostor.Server"]
