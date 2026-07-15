# syntax=docker/dockerfile:1
# Optional thin wrapper around upstream RomM.
# HVSC is NOT baked into the image. Host tree ./hvsc is bind-mounted like ./gb64
# (see docker-compose.yml → /romm/library/hvsc).
#
# Build:
#   docker compose build
# Or pull upstream only:
#   docker pull rommapp/romm:latest

ARG ROMM_BASE=rommapp/romm:latest
FROM ${ROMM_BASE}
