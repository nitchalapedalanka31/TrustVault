@echo off
set PATH=%USERPROFILE%\.dotnet;%PATH%
set DOTNET_CLI_HOME=%~dp0.dotnet_home
echo Starting TrustVault Backend API on http://localhost:5000...
dotnet run --project src\TrustVault.Api\TrustVault.Api.csproj
