FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BirthdayCommander.Core/BirthdayCommander.Core.csproj", "BirthdayCommander.Core/"]
COPY ["BirthdayCommander.Infrastructure/BirthdayCommander.Infrastructure.csproj", "BirthdayCommander.Infrastructure/"]
COPY ["BirthdayCommander.API/BirthdayCommander.API.csproj", "BirthdayCommander.API/"]
COPY ["BirthdayCommander.sln", "./"]

RUN dotnet restore "BirthdayCommander.API/BirthdayCommander.API.csproj"

COPY . .

WORKDIR "/src/BirthdayCommander.API"
RUN dotnet build "BirthdayCommander.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BirthdayCommander.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN groupadd -g 1000 appuser && \
    useradd -r -u 1000 -g appuser appuser

COPY --from=publish /app/publish .

RUN mkdir -p /app/logs && chown -R appuser:appuser /app

USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "BirthdayCommander.API.dll"]