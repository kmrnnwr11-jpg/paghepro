# ─────────────────────────────────────────────────────────────
# Backend Paghe SaaS — immagine Docker (.NET 10)
# Build context atteso: la cartella ./backend
#   docker build -t paghe-api ./backend
# ─────────────────────────────────────────────────────────────

# Stadio 1: compilazione
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia tutta la soluzione (Api dipende da Core, Persistence, Documenti)
COPY . .
RUN dotnet restore src/Paghe.Api/Paghe.Api.csproj
RUN dotnet publish src/Paghe.Api/Paghe.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stadio 2: esecuzione (immagine più leggera, solo runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Il database SQLite vive su /data: monta qui un volume/disco persistente
# (altrimenti i dati si perdono ad ogni nuovo deploy).
RUN mkdir -p /data
ENV ConnectionStrings__Default="Data Source=/data/paghe.db"

# Le piattaforme cloud (Render, Railway…) iniettano la porta in $PORT.
# Ascoltiamo lì; in locale ripieghiamo su 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet Paghe.Api.dll"]
