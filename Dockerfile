# ─────────────────────────────────────────────────────────────
# Dockerfile alla RADICE del repo — pensato per Koyeb (e simili) che
# costruiscono dal repository intero senza dover impostare una "work directory".
# È identico a backend/Dockerfile ma copia da ./backend, così su Koyeb basta:
#   New ▸ GitHub ▸ scegli paghe-saas ▸ Builder: Dockerfile ▸ Free ▸ Deploy.
# (Render continua a usare backend/Dockerfile tramite render.yaml.)
# ─────────────────────────────────────────────────────────────

# Stadio 1: compilazione
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/ .
RUN dotnet restore src/Paghe.Api/Paghe.Api.csproj
RUN dotnet publish src/Paghe.Api/Paghe.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stadio 2: esecuzione (immagine più leggera, solo runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Database SQLite su /data (effimero sul piano gratis: i dati si azzerano ai riavvii;
# per dati permanenti si collega un database, es. Supabase, via ConnectionStrings__Default).
RUN mkdir -p /data
ENV ConnectionStrings__Default="Data Source=/data/paghe.db"

# La piattaforma inietta la porta in $PORT; in locale ripieghiamo su 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet Paghe.Api.dll"]
