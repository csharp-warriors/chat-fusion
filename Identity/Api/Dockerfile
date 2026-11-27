# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
# Контекст сборки — папка Identity/ (см. compose.yaml).

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Api/ChatFusion.Identity.Api.csproj", "Api/"]
COPY ["Core/ChatFusion.Identity.Core.csproj", "Core/"]
RUN dotnet restore "./Api/ChatFusion.Identity.Api.csproj"
COPY . .
WORKDIR "/src/Api"
RUN dotnet build "./ChatFusion.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ChatFusion.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Финальный образ: chiseled (без оболочки и пакетного менеджера), запуск от пользователя без root
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
# В chiseled-образе нет curl, поэтому проверка выполняется командой самого сервиса
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "ChatFusion.Identity.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "ChatFusion.Identity.Api.dll"]
