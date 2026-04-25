FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["Integration.API/Integration.API.csproj", "Integration.API/"]
COPY ["Core.Kernel/Core.Kernel.csproj", "Core.Kernel/"]
COPY ["Core.Services/Core.Services.csproj", "Core.Services/"]
COPY ["Core.Infrastructure/Core.Infrastructure.csproj", "Core.Infrastructure/"]
RUN dotnet restore "Integration.API/Integration.API.csproj"
COPY . .
WORKDIR "/src/Integration.API"
RUN dotnet build "Integration.API.csproj" -a $TARGETARCH -c Release -o /app/build

FROM build AS publish
ARG TARGETARCH
RUN dotnet publish "Integration.API.csproj" -a $TARGETARCH -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Integration.API.dll"]
