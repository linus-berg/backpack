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

FROM base AS final
RUN apt-get -y update && apt-get install -y go-md2man libgpgme-dev libassuan-dev libbtrfs-dev pkg-config
# Install skopeo
RUN wget https://github.com/containers/skopeo/archive/refs/tags/v1.21.0.tar.gz && \
    tar -vxf v1.21.0.tar.gz && \
    cd skopeo-1.21.0 && \
    make && \
    make install-binary
#RUN apt-get -y update && apt-get install -y libgpgme-dev libassuan-dev libbtrfs-dev pkg-config
#COPY Collector.DockerArchive/skopeo /bin/skopeo
#RUN apt-get -y update && apt-get install -y skopeo

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Collector.DockerArchive.dll"]
