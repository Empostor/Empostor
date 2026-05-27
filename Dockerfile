# ============================================
# Usage:
#   1. Publish the server:
#      dotnet publish src/Empostor.Server -c Release -o publish
#   2. Publish plugins:
#      dotnet publish src/Empostor.Plugins.Welcome -c Release -o publish/plugins
#      (repeat for each plugin)
#   3. Build and run:
#      docker compose up -d
# ============================================
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy pre-published server output
COPY ./publish/ ./

# Create runtime directories
RUN mkdir -p /app/plugins /app/libraries /app/Languages /app/Log /app/config /app/Data

# Copy plugins from build output
COPY ./plugins/ ./plugins/ 2>/dev/null || true

# Copy marketplace data
COPY ./marketplace/ ./marketplace/ 2>/dev/null || true

ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV EMPOSTOR_Server__PublicIp=0.0.0.0

EXPOSE 80/tcp
EXPOSE 22023/tcp
EXPOSE 22023/udp

VOLUME ["/app/config", "/app/plugins", "/app/Languages", "/app/Log", "/app/Data"]

ENTRYPOINT ["./Empostor.Server"]
