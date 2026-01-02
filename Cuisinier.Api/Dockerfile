FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier les fichiers .csproj et restaurer les dépendances
COPY ["Cuisinier.Api/Cuisinier.Api.csproj", "Cuisinier.Api/"]
COPY ["Cuisinier.Core/Cuisinier.Core.csproj", "Cuisinier.Core/"]
COPY ["Cuisinier.Infrastructure/Cuisinier.Infrastructure.csproj", "Cuisinier.Infrastructure/"]

RUN dotnet restore "Cuisinier.Api/Cuisinier.Api.csproj"

# Copier le reste du code et compiler
COPY . .
WORKDIR "/src/Cuisinier.Api"
RUN dotnet build "Cuisinier.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cuisinier.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cuisinier.Api.dll"]

