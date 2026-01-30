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
#RUN apt-get -y update && apt-get install -y skopeo
WORKDIR /app
COPY ./skopeo /bin/skopeo
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Collector.DockerArchive.dll"]
