# ============================================
# Stage 1: Build server + plugins from source
# ============================================
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG TARGETARCH
WORKDIR /src

# Copy solution and project files first (layer caching)
COPY src/Empostor.sln ./
COPY src/Directory.Build.props ./ 2>/dev/null || true
COPY src/ProjectRules.ruleset ./ 2>/dev/null || true

# Server + API + Hazel
COPY src/Empostor.Server/Empostor.Server.csproj ./Empostor.Server/
COPY src/Empostor.Api/Empostor.Api.csproj ./Empostor.Api/
COPY src/Empostor.Api.Innersloth.Generator/Empostor.Api.Innersloth.Generator.csproj ./Empostor.Api.Innersloth.Generator/
COPY src/NextFast.Hazel-New/Next.Hazel/Next.Hazel.csproj ./NextFast.Hazel-New/Next.Hazel/
COPY src/NextFast.Hazel-New/Next.Hazel.Abstractions/Next.Hazel.Abstractions.csproj ./NextFast.Hazel-New/Next.Hazel.Abstractions/

# Plugins
COPY src/Empostor.Plugin.Chat/Empostor.Plugin.Chat.csproj ./Empostor.Plugin.Chat/
COPY src/Empostor.Plugin.Code/Empostor.Plugin.Code.csproj ./Empostor.Plugin.Code/
COPY src/Empostor.Plugin.Narrator/Empostor.Plugin.Narrator.csproj ./Empostor.Plugin.Narrator/
COPY src/Empostor.Plugins.Example/Empostor.Plugins.Example.csproj ./Empostor.Plugins.Example/
COPY src/Empostor.Plugins.FixedCode/Empostor.Plugins.FixedCode.csproj ./Empostor.Plugins.FixedCode/
COPY src/Empostor.Plugins.FriendCodeValidator/Empostor.Plugins.FriendCodeValidator.csproj ./Empostor.Plugins.FriendCodeValidator/
COPY src/Empostor.Plugins.PlayerChannel/Empostor.Plugins.PlayerChannel.csproj ./Empostor.Plugins.PlayerChannel/
COPY src/Empostor.Plugins.QqVerify/Empostor.Plugins.QqVerify.csproj ./Empostor.Plugins.QqVerify/
COPY src/Empostor.Plugins.Titles/Empostor.Plugins.Titles.csproj ./Empostor.Plugins.Titles/
COPY src/Empostor.Plugins.Welcome/Empostor.Plugins.Welcome.csproj ./Empostor.Plugins.Welcome/
COPY src/Empostor.Plugins.MapVote/Empostor.Plugins.MapVote.csproj ./Empostor.Plugins.MapVote/
COPY src/Empostor.Plugins.Message/Empostor.Plugins.Message.csproj ./Empostor.Plugins.Message/

# Restore dependencies
RUN dotnet restore Empostor.Server/Empostor.Server.csproj -a $TARGETARCH

# Copy all source
COPY src/ ./

# Publish server (self-contained single file)
RUN dotnet publish Empostor.Server/Empostor.Server.csproj \
    -c Release \
    -a $TARGETARCH \
    --self-contained false \
    -o /publish/server

# Publish plugins
RUN dotnet publish Empostor.Plugin.Chat/Empostor.Plugin.Chat.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugin.Code/Empostor.Plugin.Code.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugin.Narrator/Empostor.Plugin.Narrator.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.FixedCode/Empostor.Plugins.FixedCode.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.FriendCodeValidator/Empostor.Plugins.FriendCodeValidator.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.PlayerChannel/Empostor.Plugins.PlayerChannel.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.QqVerify/Empostor.Plugins.QqVerify.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.Titles/Empostor.Plugins.Titles.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.Welcome/Empostor.Plugins.Welcome.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.MapVote/Empostor.Plugins.MapVote.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore && \
    dotnet publish Empostor.Plugins.Message/Empostor.Plugins.Message.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /publish/plugins --no-restore

# ============================================
# Stage 2: Runtime image
# ============================================
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Create directory structure
RUN mkdir -p /app/plugins /app/libraries /app/Languages /app/Log /app/config

# Copy server
COPY --from=build /publish/server/ ./

# Copy plugins (only .dll files, skip dependency DLLs already in server)
COPY --from=build /publish/plugins/Empostor.Plugin.Chat.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugin.Code.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugin.Narrator.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.Example.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.FixedCode.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.FriendCodeValidator.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.PlayerChannel.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.QqVerify.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.Titles.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.Welcome.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.MapVote.dll ./plugins/
COPY --from=build /publish/plugins/Empostor.Plugins.Message.dll ./plugins/

# Copy marketplace data
COPY marketplace/ ./marketplace/

ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV EMPOSTOR_Server__PublicIp=0.0.0.0

EXPOSE 80/tcp
EXPOSE 22023/tcp
EXPOSE 22023/udp

VOLUME ["/app/config", "/app/plugins", "/app/Languages", "/app/Log", "/app/marketplace"]

ENTRYPOINT ["./Empostor.Server"]
