param([Parameter(Mandatory=$true)][string]$TorchDir)
$ErrorActionPreference='Stop'
$seDir=Join-Path $TorchDir 'DedicatedServer64'
dotnet restore .\src\TorchPlugin\FriendlyGridAccess.csproj
dotnet build .\src\TorchPlugin\FriendlyGridAccess.csproj -c Release --no-restore "-p:TorchDir=$TorchDir" "-p:SEDir=$seDir" "-p:Version=0.5.0"
Write-Host 'Build complete. For hosted servers, prefer the GitHub Actions workflow.'
