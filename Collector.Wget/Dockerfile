FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["Collector.Wget/Collector.Wget.csproj", "Collector.Wget/"]
COPY ["Collector.Kernel/Collector.Kernel.csproj", "Collector.Kernel/"]
COPY ["Core.Kernel/Core.Kernel.csproj", "Core.Kernel/"]
RUN dotnet restore "Collector.Wget/Collector.Wget.csproj"
COPY . .
WORKDIR "/src/Collector.Wget"
RUN dotnet build "Collector.Wget.csproj" -a $TARGETARCH -c Release -o /app/build

FROM build AS publish
ARG TARGETARCH
RUN dotnet publish "Collector.Wget.csproj" -a $TARGETARCH -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Collector.Wget.dll"]
