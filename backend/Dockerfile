# ERPOS API — multi-stage build. Run migrations with: docker run … erpos-api --migrate
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first (cached until a project file changes).
COPY src/Erpos.Domain/Erpos.Domain.csproj src/Erpos.Domain/
COPY src/Erpos.Application/Erpos.Application.csproj src/Erpos.Application/
COPY src/Erpos.Infrastructure/Erpos.Infrastructure.csproj src/Erpos.Infrastructure/
COPY src/Erpos.Api/Erpos.Api.csproj src/Erpos.Api/
RUN dotnet restore src/Erpos.Api/Erpos.Api.csproj
COPY src/ src/
RUN dotnet publish src/Erpos.Api/Erpos.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Built-in non-root user of the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Erpos.Api.dll"]
