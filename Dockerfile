# ─────────────────────────────────────────────────────────────────────────────
# Kahvi API — Dockerfile multi-stage
# Etapa 1: build (compila y publica). Etapa 2: runtime ligero (solo lo necesario).
# Produce una imagen pequeña y portable (Railway, Render, Fly.io, etc.).
# ─────────────────────────────────────────────────────────────────────────────

# ---- Etapa de build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo los csproj primero para aprovechar la caché de restore.
COPY Chiron.sln ./
COPY src/Chiron.Domain/Chiron.Domain.csproj src/Chiron.Domain/
COPY src/Chiron.Application/Chiron.Application.csproj src/Chiron.Application/
COPY src/Chiron.Infrastructure/Chiron.Infrastructure.csproj src/Chiron.Infrastructure/
COPY src/Chiron.Api/Chiron.Api.csproj src/Chiron.Api/
COPY src/Chiron.ConsoleApp/Chiron.ConsoleApp.csproj src/Chiron.ConsoleApp/
RUN dotnet restore src/Chiron.Api/Chiron.Api.csproj

# Copiar el resto del código y publicar la API en modo Release.
COPY . .
RUN dotnet publish src/Chiron.Api/Chiron.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Etapa de runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway (y otros PaaS) inyectan el puerto por la variable PORT.
# ASP.NET Core escuchará en ese puerto.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Chiron.Api.dll"]
