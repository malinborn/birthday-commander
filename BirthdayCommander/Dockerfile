FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BirthdayCommander/BirthdayCommander.csproj", "BirthdayCommander/"]
RUN dotnet restore "BirthdayCommander/BirthdayCommander.csproj"
COPY . .
WORKDIR "/src/BirthdayCommander"
RUN dotnet build "./BirthdayCommander.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BirthdayCommander.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BirthdayCommander.dll"]
