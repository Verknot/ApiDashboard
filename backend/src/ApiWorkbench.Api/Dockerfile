# Build from repo root:
#   docker build -f backend/src/ApiWorkbench.Api/Dockerfile -t api-workbench-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY backend/src/ApiWorkbench.Api/ApiWorkbench.Api.csproj backend/src/ApiWorkbench.Api/
RUN dotnet restore backend/src/ApiWorkbench.Api/ApiWorkbench.Api.csproj

COPY backend/src/ApiWorkbench.Api/ backend/src/ApiWorkbench.Api/
COPY config/ config/
RUN dotnet publish backend/src/ApiWorkbench.Api/ApiWorkbench.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    ServicesConfigPath=/app/config/services.yaml

RUN mkdir -p /app/config /certs \
    && chown -R $APP_UID:$APP_UID /app /certs

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
COPY --from=build --chown=$APP_UID:$APP_UID /src/config/ /app/config/

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiWorkbench.Api.dll"]
