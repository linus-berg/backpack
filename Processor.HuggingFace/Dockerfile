FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Processor.HuggingFace/Processor.HuggingFace.csproj", "Processor.HuggingFace/"]
COPY ["Core.Infrastructure/Core.Infrastructure.csproj", "Core.Infrastructure/"]
COPY ["Core.Services/Core.Services.csproj", "Core.Services/"]
COPY ["Core.Kernel/Core.Kernel.csproj", "Core.Kernel/"]
RUN dotnet restore "Processor.HuggingFace/Processor.HuggingFace.csproj" -a $TARGETARCH
COPY . .
WORKDIR "/src/Processor.HuggingFace"
RUN dotnet build "./Processor.HuggingFace.csproj" -a $TARGETARCH -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Processor.HuggingFace.csproj" -a $TARGETARCH -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Processor.HuggingFace.dll"]
