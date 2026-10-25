# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["src/TrustVault.Core/TrustVault.Core.csproj", "src/TrustVault.Core/"]
COPY ["src/TrustVault.Infrastructure/TrustVault.Infrastructure.csproj", "src/TrustVault.Infrastructure/"]
COPY ["src/TrustVault.Api/TrustVault.Api.csproj", "src/TrustVault.Api/"]

RUN dotnet restore "src/TrustVault.Api/TrustVault.Api.csproj"

# Copy source and publish
COPY . .
WORKDIR "/src/src/TrustVault.Api"
RUN dotnet publish "TrustVault.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create dedicated storage directory outside web root with least privilege
RUN mkdir -p /var/trustvault/storage && chown -R $APP_UID:$APP_UID /var/trustvault/storage

USER $APP_UID
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV Storage__LocalPath=/var/trustvault/storage
EXPOSE 5000

ENTRYPOINT ["dotnet", "TrustVault.Api.dll"]
