# syntax=docker/dockerfile:1
# RomM image with embedded High Voltage SID Collection (HVSC).
# HVSC is fetched with the C# tool tools/HvscFetch (no Python/bash scripts).
#
# Build:
#   docker build -t romm-gb64:local .
# Optional:
#   docker build --build-arg HVSC_URL=https://hvsc.brona.dk/HVSC/HVSC_85-all-of-them.7z -t romm-gb64:local .

ARG ROMM_BASE=rommapp/romm:latest

# -----------------------------------------------------------------------------
# Stage: fetch + normalize HVSC (C#)
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS hvsc
WORKDIR /src
COPY tools/HvscFetch/HvscFetch.csproj tools/HvscFetch/
RUN dotnet restore tools/HvscFetch/HvscFetch.csproj
COPY tools/HvscFetch/ tools/HvscFetch/
ARG HVSC_URL=
ENV HVSC_URL=${HVSC_URL}
RUN mkdir -p /out/hvsc /tmp/hvsc-work \
    && HVSC_WORK_DIR=/tmp/hvsc-work dotnet run --project tools/HvscFetch/HvscFetch.csproj -c Release --no-launch-profile -- /out/hvsc

# -----------------------------------------------------------------------------
# Stage: RomM runtime with HVSC embedded
# -----------------------------------------------------------------------------
FROM ${ROMM_BASE}

# HVSC beside roms/ so Structure A is not polluted by MUSICIANS/ folders.
#   SID: MUSICIANS\W\Whittaker_David\180.sid
#   ->  /romm/library/hvsc/MUSICIANS/W/Whittaker_David/180.sid
COPY --from=hvsc /out/hvsc /romm/library/hvsc

USER root
RUN test -d /romm/library/hvsc/MUSICIANS \
    && test -f /romm/library/hvsc/.hvsc-origin.txt \
    && echo "HVSC embed OK: $(find /romm/library/hvsc -type f -iname '*.sid' | wc -l) sid files"

# Note: do not bind-mount an empty host path over /romm/library or HVSC disappears.
# Mutable RomM state should use /romm/assets, /romm/config, and DB volumes only.
