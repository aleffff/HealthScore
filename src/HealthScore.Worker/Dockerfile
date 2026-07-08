FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/HealthScore.Worker/HealthScore.Worker.csproj \
    && dotnet publish src/HealthScore.Worker/HealthScore.Worker.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" --uid 10001 appuser
COPY --from=build /app .
USER appuser
ENTRYPOINT ["dotnet", "HealthScore.Worker.dll"]
