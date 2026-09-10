# Base runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj (agora dentro de /src)
COPY ["src/NecroWiKi.API/NecroWiKi.API.csproj", "src/NecroWiKi.API/"]
COPY ["src/NecroWiKi.Application/NecroWiKi.Application.csproj", "src/NecroWiKi.Application/"]
COPY ["src/NecroWiKi.Infrastructure/NecroWiKi.Infrastructure.csproj", "src/NecroWiKi.Infrastructure/"]

# Restore
RUN dotnet restore "src/NecroWiKi.API/NecroWiKi.API.csproj"

# Copy everything
COPY . .

# Build
RUN dotnet build "src/NecroWiKi.API/NecroWiKi.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/NecroWiKi.API/NecroWiKi.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NecroWiKi.API.dll"]