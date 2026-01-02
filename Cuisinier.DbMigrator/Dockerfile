FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier les fichiers .csproj et restaurer les dépendances
COPY ["Cuisinier.DbMigrator/Cuisinier.DbMigrator.csproj", "Cuisinier.DbMigrator/"]
COPY ["Cuisinier.Core/Cuisinier.Core.csproj", "Cuisinier.Core/"]
COPY ["Cuisinier.Infrastructure/Cuisinier.Infrastructure.csproj", "Cuisinier.Infrastructure/"]

RUN dotnet restore "Cuisinier.DbMigrator/Cuisinier.DbMigrator.csproj"

# Copier le reste du code et compiler
COPY . .
WORKDIR "/src/Cuisinier.DbMigrator"
RUN dotnet build "Cuisinier.DbMigrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cuisinier.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cuisinier.DbMigrator.dll"]

