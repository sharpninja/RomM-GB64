# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/CsdbBridge/CsdbBridge.csproj src/CsdbBridge/
RUN dotnet restore src/CsdbBridge/CsdbBridge.csproj
COPY src/CsdbBridge/ src/CsdbBridge/
RUN dotnet publish src/CsdbBridge/CsdbBridge.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8090 \
    LIBRARY_ROMS_ROOT=/data/roms \
    HVSC_ROOT=/romm/library/hvsc \
    CSDB_DATA_ROOT=/data/csdb
COPY --from=build /app/publish .
RUN mkdir -p /data/roms /data/csdb/rss
EXPOSE 8090
ENTRYPOINT ["dotnet", "CsdbBridge.dll"]
