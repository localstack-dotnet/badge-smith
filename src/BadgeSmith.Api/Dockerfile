# syntax=docker/dockerfile:1.7

###############################
# Stage 0: Build (Native AOT)
###############################
ARG DOTNET_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build

# Build args (override at build time if needed)
ARG PROJECT=src/BadgeSmith.Api/BadgeSmith.Api.csproj
ARG CONFIG=Release
ARG RID=linux-x64
ARG PUBLISH_DIR=/artifacts/publish
ARG MSTAT=false

# Tooling needed for NativeAOT link
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev zip \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY . .

# Use BuildKit caches to speed up restore/publish between builds
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore ${PROJECT} \
    -p:EnableSourceControlManagerQueries=false \
    -p:EmbedUntrackedSources=false

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish ${PROJECT} -c ${CONFIG} -r ${RID} --self-contained true \
      -p:PublishAot=true -p:StripSymbols=true -p:DebugType=none -p:EnableTelemetry=false -p:EnableLocalStack=false \
      -p:IlcGenerateMstatFile=${MSTAT} \
      -p:EnableSourceControlManagerQueries=false \
      -p:EmbedUntrackedSources=false \
      -o ${PUBLISH_DIR} \
 && chmod +x ${PUBLISH_DIR}/bootstrap

RUN if [ "$MSTAT" = "true" ]; then \
      find /src -name '*.mstat' -exec cp {} ${PUBLISH_DIR}/bootstrap.mstat \; ; \
    fi

###############################
# Stage 1: Lambda Container Image
###############################
FROM public.ecr.aws/lambda/provided:al2023 AS lambda-image
WORKDIR /var/runtime
COPY --from=build /artifacts/publish/bootstrap .
CMD ["bootstrap"]

###############################
# Stage 2: Zip Artifact for "Upload .zip"
###############################
FROM debian:bookworm-slim AS lambda-zip
ARG RID=linux-x64
ARG ZIP_NAME=badge-lambda-${RID}.zip
RUN apt-get update && apt-get install -y --no-install-recommends python3 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /out
COPY --from=build /artifacts/publish/bootstrap ./bootstrap
RUN python3 - "${ZIP_NAME}" <<'PY'
import os
import sys
import zipfile

entry = zipfile.ZipInfo.from_file("bootstrap", arcname="bootstrap")
source_mtime = (*entry.date_time[:5], entry.date_time[5] - entry.date_time[5] % 2)
entry.create_system = 3
entry.external_attr = 0o100755 << 16
with open("bootstrap", "rb") as source, zipfile.ZipFile(sys.argv[1], "w") as archive:
    archive.writestr(entry, source.read(), compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)

with zipfile.ZipFile(sys.argv[1]) as archive:
    if archive.namelist() != ["bootstrap"]:
        raise SystemExit("ZIP must contain only bootstrap")
    if archive.testzip() is not None:
        raise SystemExit("ZIP CRC verification failed")
    entry = archive.getinfo("bootstrap")
    if entry.create_system != 3 or entry.external_attr >> 16 != 0o100755:
        raise SystemExit("bootstrap must be Unix 0100755")
    if entry.file_size != os.path.getsize("bootstrap"):
        raise SystemExit("bootstrap size mismatch")
    if entry.compress_type != zipfile.ZIP_DEFLATED:
        raise SystemExit("ZIP compression must be Deflate")
    if entry.date_time != source_mtime:
        raise SystemExit("bootstrap mtime mismatch")
PY

###############################
# Stage 3: Export-only (avoid rootfs & symlinks)
###############################
FROM scratch AS export-zip
ARG RID=linux-x64
ARG ZIP_NAME=badge-lambda-${RID}.zip
COPY --from=lambda-zip /out/${ZIP_NAME} /${ZIP_NAME}

###############################
# Stage 4: Export mstat (build with --build-arg MSTAT=true)
###############################
FROM scratch AS export-mstat
COPY --from=build /artifacts/publish/bootstrap.mstat /bootstrap.mstat
