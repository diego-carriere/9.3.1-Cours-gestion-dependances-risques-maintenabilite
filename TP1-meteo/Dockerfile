# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Les manifestes d'abord, sans le code source : `dotnet restore` reste en cache tant
# qu'aucune version de paquet ne change, même si le code applicatif change ensuite. Seuls
# les projets nécessaires à Meteo.Api sont copiés/restaurés (pas les projets de test).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Meteo.Domain/Meteo.Domain.csproj src/Meteo.Domain/
COPY src/Meteo.Application/Meteo.Application.csproj src/Meteo.Application/
COPY src/Meteo.Infrastructure/Meteo.Infrastructure.csproj src/Meteo.Infrastructure/
COPY src/Meteo.Api/Meteo.Api.csproj src/Meteo.Api/
RUN dotnet restore src/Meteo.Api/Meteo.Api.csproj

COPY src/ src/
RUN dotnet publish src/Meteo.Api/Meteo.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# Utilisateur non-root fourni par l'image de base .NET.
USER $APP_UID

# Aucun User-Agent Nominatim par défaut : voir NominatimOptions, la validation au démarrage
# refuse de lancer l'app sans -e Nominatim__UserAgent="...".
ENTRYPOINT ["dotnet", "Meteo.Api.dll"]
