# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS admin
WORKDIR /src/admin
ENV npm_config_fund=false \
    npm_config_audit=false \
    npm_config_update_notifier=false
COPY admin/package.json admin/package-lock.json ./
# Lockfile may resolve through a private npm proxy. Point it at the public registry.
RUN sed -i -E 's#https://[^/]+/artifactory/api/npm/npm-remote/#https://registry.npmjs.org/#g' package-lock.json \
    && npm ci --registry=https://registry.npmjs.org/ --no-fund --no-audit
COPY admin/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/AvaEntra.Server.csproj server/
# Repo nuget.config is dockerignored. Restore only from public nuget.org.
RUN printf '%s\n' \
      '<?xml version="1.0" encoding="utf-8"?>' \
      '<configuration>' \
      '  <packageSources>' \
      '    <clear />' \
      '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />' \
      '  </packageSources>' \
      '</configuration>' > nuget.config \
    && dotnet restore server/AvaEntra.Server.csproj --configfile nuget.config --source https://api.nuget.org/v3/index.json
COPY server/ server/
COPY --from=admin /src/server/wwwroot server/wwwroot
RUN dotnet publish server/AvaEntra.Server.csproj -c Release -o /app --no-restore --configfile nuget.config

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/data && chown -R $APP_UID /app/data
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
VOLUME /app/data
ENTRYPOINT ["dotnet", "AvaEntra.Server.dll"]
