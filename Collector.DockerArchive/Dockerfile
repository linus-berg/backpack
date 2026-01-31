FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH

WORKDIR /src
COPY ["Collector.DockerArchive/Collector.DockerArchive.csproj", "Collector.DockerArchive/"]
COPY ["Collector.Kernel/Collector.Kernel.csproj", "Collector.Kernel/"]
COPY ["Core.Kernel/Core.Kernel.csproj", "Core.Kernel/"]
COPY ["Library.Skopeo/Library.Skopeo.csproj", "Library.Skopeo/"]
RUN dotnet restore "Collector.DockerArchive/Collector.DockerArchive.csproj"
COPY . .
WORKDIR "/src/Collector.DockerArchive"
RUN dotnet build "Collector.DockerArchive.csproj" -a $TARGETARCH -c Release -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Collector.DockerArchive.csproj" -a $TARGETARCH -c Release -o /app/publish /p:UseAppHost=false

# Build static skopeo binary (this is unsupported)
# Requires newer debian release than 8.0 dotnet image has
# https://github.com/containers/skopeo/blob/main/install.md#building-a-static-binary
FROM docker.io/ubuntu:24.04 AS skopeo-build
# Set skopeo version here
ARG SKOPEO_VERSION=1.21.0
WORKDIR /src

RUN apt-get -y update
RUN apt-get install -y ca-certificates wget golang-go make go-md2man libgpgme-dev libassuan-dev libbtrfs-dev pkg-config
ADD https://github.com/containers/skopeo/archive/refs/tags/v${SKOPEO_VERSION}.tar.gz .
RUN tar -vxf v${SKOPEO_VERSION}.tar.gz && \
    cd skopeo-${SKOPEO_VERSION} && \
    export CGO_ENABLED=0 && \
    export BUILDTAGS=containers_image_openpgp && \
    make
# Copy skopeo to output dir
RUN mkdir /output/
RUN cp skopeo-${SKOPEO_VERSION}/bin/skopeo /output/skopeo

FROM base AS final
WORKDIR /app
COPY Collector.DockerArchive/policy.json /etc/containers/policy.json
COPY --from=skopeo-build /output/skopeo /bin/skopeo
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Collector.DockerArchive.dll"]
