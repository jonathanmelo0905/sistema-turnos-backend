# Imagen de la API. Dos etapas: se compila con el SDK (pesado) y se copia
# solo el resultado a la imagen del runtime (liviana).

# ---- Etapa 1: compilar ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Primero solo el .csproj: si no cambian los paquetes, Docker reutiliza la capa del restore
COPY AgendaApp.csproj .
RUN dotnet restore AgendaApp.csproj
COPY . .
RUN dotnet publish AgendaApp.csproj -c Release -o /app --no-restore

# ---- Etapa 2: ejecutar ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Carpeta de archivos subidos (fotos, logos, portadas). La imagen corre con el usuario
# "app" (no root), así que debe ser su dueño; el volumen de Docker hereda ese permiso.
USER root
RUN mkdir -p wwwroot/archivos && chown -R app:app wwwroot
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgendaApp.dll"]
