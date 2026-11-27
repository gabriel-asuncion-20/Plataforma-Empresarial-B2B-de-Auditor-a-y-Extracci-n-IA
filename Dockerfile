# ==============================================================================
# AuditAI B2B Backend — Multi-Stage Production Dockerfile (.NET 10)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AuditAI.sln", "./"]
COPY ["src/AuditAI.Domain/AuditAI.Domain.csproj", "src/AuditAI.Domain/"]
COPY ["src/AuditAI.Application/AuditAI.Application.csproj", "src/AuditAI.Application/"]
COPY ["src/AuditAI.Infrastructure/AuditAI.Infrastructure.csproj", "src/AuditAI.Infrastructure/"]
COPY ["src/AuditAI.Api/AuditAI.Api.csproj", "src/AuditAI.Api/"]

RUN dotnet restore "src/AuditAI.Api/AuditAI.Api.csproj"

COPY src/ src/
WORKDIR "/src/src/AuditAI.Api"
RUN dotnet publish "AuditAI.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/storage/uploads /app/sample-pdfs && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AuditAI.Api.dll"]
