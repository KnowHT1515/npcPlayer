param(
    [Parameter(Mandatory = $true)]
    [string]$CelesteDir
)

$ErrorActionPreference = "Stop"
$ProjectPath = Join-Path $PSScriptRoot "npcPlayer.csproj"
$BuildDirectory = Join-Path $PSScriptRoot "build"
$IntermediateDirectory = Join-Path $PSScriptRoot "obj"

$requiredAssemblies = @("Celeste.dll", "MMHOOK_Celeste.dll", "FNA.dll")
foreach ($assembly in $requiredAssemblies) {
    if (-not (Test-Path (Join-Path $CelesteDir $assembly))) {
        throw "$assembly not found in '$CelesteDir'. Pass the active Everest Celeste directory."
    }
}

foreach ($directory in @($BuildDirectory, $IntermediateDirectory)) {
    if (Test-Path $directory) {
        Remove-Item $directory -Recurse -Force
    }
}

& dotnet build $ProjectPath --configuration Release --output $BuildDirectory --no-incremental "-p:CelesteDir=$CelesteDir" "-p:PathMap=$PSScriptRoot=/_/npcPlayer"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$CodeDirectory = Join-Path $PSScriptRoot "Code"
[IO.Directory]::CreateDirectory($CodeDirectory) | Out-Null
Copy-Item (Join-Path $BuildDirectory "npcPlayer.dll") (Join-Path $CodeDirectory "npcPlayer.dll") -Force
Copy-Item (Join-Path $BuildDirectory "npcPlayer.pdb") (Join-Path $CodeDirectory "npcPlayer.pdb") -Force

Write-Host ""
Write-Host "Build complete and Code/ synchronized: $BuildDirectory\npcPlayer.dll"
