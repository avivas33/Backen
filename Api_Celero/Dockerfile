# Dockerfile para Api_Celero
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 7262

# Variables de entorno por defecto
ENV ASPNETCORE_URLS=http://+:7262
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY ["Api_Celero.csproj", "./"]
RUN dotnet restore "Api_Celero.csproj"

# Copiar el resto del código y compilar
COPY . .
RUN dotnet build "Api_Celero.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Api_Celero.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Crear directorio para SQLite y establecer permisos
RUN mkdir -p /app/data && chmod 777 /app/data

# Crear usuario no-root
RUN addgroup --group dotnet --gid 1000 && \
    adduser --uid 1000 --ingroup dotnet --disabled-password --gecos "" dotnet && \
    chown -R dotnet:dotnet /app

USER dotnet

ENTRYPOINT ["dotnet", "Api_Celero.dll"]
