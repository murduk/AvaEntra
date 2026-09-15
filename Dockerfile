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
# Repo nuget.config is dockerignored. Never copy it into the image.
COPY server/AvaEntra.Server.csproj server/
RUN dotnet restore server/AvaEntra.Server.csproj \
      --source https://api.nuget.org/v3/index.json \
      --ignore-failed-sources
COPY server/ server/
COPY --from=admin /src/server/wwwroot server/wwwroot
RUN dotnet publish server/AvaEntra.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/storage && chown -R $APP_UID /app/storage
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
VOLUME /app/storage
ENTRYPOINT ["dotnet", "AvaEntra.Server.dll"]
