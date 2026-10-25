@echo off
set PATH=%USERPROFILE%\.dotnet;%PATH%
set DOTNET_CLI_HOME=%~dp0.dotnet_home
echo Running TrustVault Automated Security Integration Tests...
dotnet test tests\TrustVault.Tests\TrustVault.Tests.csproj --verbosity normal
pause
