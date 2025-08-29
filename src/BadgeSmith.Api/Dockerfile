# syntax=docker/dockerfile:1.7

###############################
# Stage 0: Build (Native AOT)
###############################
ARG DOTNET_VERSION=9.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build

# Build args (override at build time if needed)
ARG PROJECT=src/BadgeSmith.Api/BadgeSmith.Api.csproj
ARG CONFIG=Release
ARG RID=linux-x64
ARG PUBLISH_DIR=/artifacts/publish

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
      -p:PublishAot=true -p:StripSymbols=true -p:DebugType=none -p:EnableTelemetry=false \
      -p:EnableSourceControlManagerQueries=false \
      -p:EmbedUntrackedSources=false \
      -o ${PUBLISH_DIR} \
 && chmod +x ${PUBLISH_DIR}/bootstrap

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
RUN apt-get update && apt-get install -y --no-install-recommends zip \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /out
COPY --from=build /artifacts/publish/bootstrap ./bootstrap
RUN zip -9 -r ${ZIP_NAME} bootstrap

###############################
# Stage 3: Export-only (avoid rootfs & symlinks)
###############################
FROM scratch AS export-zip
ARG RID=linux-x64
ARG ZIP_NAME=badge-lambda-${RID}.zip
COPY --from=lambda-zip /out/${ZIP_NAME} /${ZIP_NAME}
