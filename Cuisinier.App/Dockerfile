FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier les fichiers .csproj et restaurer les dépendances
COPY ["Cuisinier.App/Cuisinier.App.csproj", "Cuisinier.App/"]
COPY ["Cuisinier.Core/Cuisinier.Core.csproj", "Cuisinier.Core/"]

RUN dotnet restore "Cuisinier.App/Cuisinier.App.csproj"

# Copier le reste du code et compiler
COPY . .
WORKDIR "/src/Cuisinier.App"
RUN dotnet build "Cuisinier.App.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cuisinier.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cuisinier.App.dll"]

