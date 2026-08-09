# ── Build ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Se copian primero los .csproj para aprovechar la cache de capas:
# si no cambian las dependencias, no se vuelve a hacer restore.
COPY InventarioPro.sln .
COPY src/InventarioPro.Api/InventarioPro.Api.csproj src/InventarioPro.Api/
COPY tests/InventarioPro.Tests/InventarioPro.Tests.csproj tests/InventarioPro.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/InventarioPro.Api/InventarioPro.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Usuario sin privilegios: si alguien logra ejecutar código en el contenedor,
# no lo hace como root.
RUN adduser --disabled-password --gecos "" --uid 10001 appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "InventarioPro.Api.dll"]
