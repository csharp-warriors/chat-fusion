# Мигратор базы данных Identity: создаёт базу данных, применяет миграции, создаёт первого администратора и завершается.
# Контекст сборки — папка Identity/ (см. compose.yaml):
#   docker build -f Migrator/Dockerfile -t chatfusion/identity-migrator .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Core/ChatFusion.Identity.Core.csproj", "Core/"]
COPY ["Migrator/ChatFusion.Identity.Migrator.csproj", "Migrator/"]
RUN dotnet restore "./Migrator/ChatFusion.Identity.Migrator.csproj"
COPY . .
WORKDIR "/src/Migrator"
RUN dotnet publish "./ChatFusion.Identity.Migrator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Финальный образ: среда выполнения .NET без ASP.NET Core, chiseled (без оболочки), запуск от пользователя без root
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ChatFusion.Identity.Migrator.dll"]
CMD ["migrate"]
