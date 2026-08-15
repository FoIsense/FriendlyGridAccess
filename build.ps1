param(
    [Parameter(Mandatory = $false)]
    [string]$TorchDir = "C:\Torch"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$PluginName = "FriendlyGridAccess"
$PluginVersion = "0.4.0"
$PluginGuid = "26f55d62-7b65-4e78-a347-dabf640d66d1"

function Fail([string]$Message) {
    Write-Host ""
    Write-Host "BUILD FAILED: $Message" -ForegroundColor Red
    exit 1
}

$TorchDir = [System.IO.Path]::GetFullPath($TorchDir.Trim('"'))
Write-Host "Friendly Grid Access - Torch package builder" -ForegroundColor Cyan
Write-Host "Torch folder: $TorchDir"

if (-not (Test-Path $TorchDir -PathType Container)) {
    Fail "Torch folder not found: $TorchDir`nExample: .\build.ps1 -TorchDir 'D:\Torch'"
}

$TorchApi = Get-ChildItem -Path $TorchDir -Filter "Torch.API.dll" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $TorchApi) { Fail "Torch.API.dll was not found anywhere under $TorchDir" }
$TorchReferenceDir = $TorchApi.DirectoryName

$PreferredSEDirs = @(
    (Join-Path $TorchDir "DedicatedServer64"),
    (Join-Path $TorchDir "SpaceEngineersDedicatedServer\DedicatedServer64"),
    (Join-Path $TorchDir "Server\DedicatedServer64")
)
$SEDir = $null
foreach ($candidate in $PreferredSEDirs) {
    if (Test-Path (Join-Path $candidate "Sandbox.Game.dll") -PathType Leaf) { $SEDir = $candidate; break }
}
if ($null -eq $SEDir) {
    $SandboxGame = Get-ChildItem -Path $TorchDir -Filter "Sandbox.Game.dll" -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "DedicatedServer64" } | Select-Object -First 1
    if ($null -ne $SandboxGame) { $SEDir = $SandboxGame.DirectoryName }
}
if ($null -eq $SEDir) { Fail "Could not find DedicatedServer64/Sandbox.Game.dll under $TorchDir" }

Write-Host "Torch references: $TorchReferenceDir"
Write-Host "SE references:    $SEDir"

$RequiredTorch = @("Torch.API.dll", "Torch.dll", "NLog.dll")
foreach ($file in $RequiredTorch) {
    $candidate = Join-Path $TorchReferenceDir $file
    if (-not (Test-Path $candidate -PathType Leaf)) {
        if ($file -eq "NLog.dll" -and (Test-Path (Join-Path $TorchDir $file) -PathType Leaf)) {
            # Do not modify the Torch install; just point the project at Torch root when needed.
            $TorchReferenceDir = $TorchDir
        } else {
            Fail "Missing required Torch assembly: $file"
        }
    }
}

$RequiredSE = @("Sandbox.Common.dll", "Sandbox.Game.dll", "SpaceEngineers.Game.dll", "VRage.dll", "VRage.Game.dll", "VRage.Library.dll", "VRage.Math.dll")
foreach ($file in $RequiredSE) {
    if (-not (Test-Path (Join-Path $SEDir $file) -PathType Leaf)) { Fail "Missing required Space Engineers assembly: $file in $SEDir" }
}

$Project = Join-Path $PSScriptRoot "src\FriendlyGridAccess\FriendlyGridAccess.csproj"
$ManifestTemplate = Join-Path $PSScriptRoot "manifest.xml"
if (-not (Test-Path $Project -PathType Leaf)) { Fail "Project file not found: $Project" }
if (-not (Test-Path $ManifestTemplate -PathType Leaf)) { Fail "manifest.xml not found: $ManifestTemplate" }
if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail "dotnet was not found. Install Visual Studio 2022 Build Tools / .NET SDK and the .NET Framework 4.8.1 Developer Pack."
}

$Dist = Join-Path $PSScriptRoot "dist"
$PackageDir = Join-Path $Dist "package"
$PackageZip = Join-Path $Dist "$PluginName.zip"
if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null

Write-Host ""
Write-Host "Restoring packages..." -ForegroundColor Yellow
& dotnet restore $Project
if ($LASTEXITCODE -ne 0) { Fail "NuGet restore failed." }

Write-Host ""
Write-Host "Building Release..." -ForegroundColor Yellow
& dotnet build $Project -c Release --no-restore -p:TorchDir="$TorchReferenceDir" -p:SEDir="$SEDir"
if ($LASTEXITCODE -ne 0) { Fail "Compilation failed. Review the compiler errors above." }

$OutputDir = Join-Path $PSScriptRoot "src\FriendlyGridAccess\bin\Release\net481"
$PluginDll = Join-Path $OutputDir "$PluginName.dll"
if (-not (Test-Path $PluginDll -PathType Leaf)) { Fail "Build succeeded but $PluginName.dll was not found at $PluginDll" }

# Torch plugin ZIP: manifest.xml MUST be inside the ZIP, next to the DLL.
Copy-Item $PluginDll $PackageDir -Force
$Pdb = Join-Path $OutputDir "$PluginName.pdb"
if (Test-Path $Pdb -PathType Leaf) { Copy-Item $Pdb $PackageDir -Force }

# Harmony is a plugin runtime dependency. NuGet normally emits 0Harmony.dll.
$HarmonyCandidates = @(
    (Join-Path $OutputDir "0Harmony.dll"),
    (Join-Path $OutputDir "Lib.Harmony.dll")
)
$HarmonyCopied = $false
foreach ($h in $HarmonyCandidates) {
    if (Test-Path $h -PathType Leaf) { Copy-Item $h $PackageDir -Force; $HarmonyCopied = $true; break }
}
if (-not $HarmonyCopied) {
    Write-Host "WARNING: Harmony DLL was not found in build output. Torch may already provide it, but verify server logs." -ForegroundColor Yellow
}

# Newtonsoft.Json is often supplied by Torch, but package a local copy when NuGet produced one.
$Json = Join-Path $OutputDir "Newtonsoft.Json.dll"
if (Test-Path $Json -PathType Leaf) { Copy-Item $Json $PackageDir -Force }

$ManifestText = Get-Content $ManifestTemplate -Raw
$ManifestText = $ManifestText.Replace('${VERSION}', $PluginVersion)
Set-Content -Path (Join-Path $PackageDir "manifest.xml") -Value $ManifestText -Encoding UTF8

# Validate manifest basics before packaging.
[xml]$ManifestXml = Get-Content (Join-Path $PackageDir "manifest.xml")
if ($ManifestXml.PluginManifest.Name -ne $PluginName) { Fail "manifest.xml plugin name does not match $PluginName" }
if ($ManifestXml.PluginManifest.Guid -ne $PluginGuid) { Fail "manifest.xml GUID does not match expected GUID $PluginGuid" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $PackageZip) { Remove-Item $PackageZip -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($PackageDir, $PackageZip)

# Also copy the DLL to dist root for manual/debug installation.
Copy-Item $PluginDll (Join-Path $Dist "$PluginName.dll") -Force
Copy-Item (Join-Path $PackageDir "manifest.xml") (Join-Path $Dist "manifest.xml") -Force

Write-Host ""
Write-Host "BUILD SUCCESS" -ForegroundColor Green
Write-Host ""
Write-Host "Torch-ready plugin ZIP:" -ForegroundColor Cyan
Write-Host "  $PackageZip"
Write-Host ""
Write-Host "Plugin GUID:" -ForegroundColor Cyan
Write-Host "  $PluginGuid"
Write-Host ""
Write-Host "INSTALL:" -ForegroundColor Yellow
Write-Host "  1. Stop Torch."
Write-Host "  2. Copy $PluginName.zip into Torch\Plugins\."
Write-Host "  3. If your host requires a plugin GUID in Torch.cfg, add:"
Write-Host "     <guid>$PluginGuid</guid>"
Write-Host "  4. Restart Torch and check the log for 'FriendlyGridAccess loaded'."
Write-Host ""
Write-Host "IMPORTANT: Do not use the GitHub .git source URL as the actual plugin binary unless your host explicitly builds Torch plugins from source."
