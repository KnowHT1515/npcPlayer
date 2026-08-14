param(
    [Parameter(Mandatory = $true)]
    [string]$CelesteDir
)

$ErrorActionPreference = "Stop"
$ProjectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$DistDirectory = [IO.Path]::GetFullPath((Join-Path $ProjectRoot "dist"))
$StagingDirectory = [IO.Path]::GetFullPath((Join-Path $DistDirectory "npcPlayer-package"))
$EverestYamlPath = Join-Path $ProjectRoot "everest.yaml"
$EverestYaml = [IO.File]::ReadAllText($EverestYamlPath)
$VersionMatch = [regex]::Match($EverestYaml, '(?m)^  Version:\s*([^\s#]+)\s*$')
if (-not $VersionMatch.Success) {
    throw "Could not read the npcPlayer version from everest.yaml."
}
$PackageVersion = $VersionMatch.Groups[1].Value
$ArchivePath = [IO.Path]::GetFullPath((Join-Path $DistDirectory "npcPlayer-$PackageVersion.zip"))

function Assert-ProjectPath([string]$Path) {
    $prefix = $ProjectRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside project: $Path"
    }
}

foreach ($path in @($DistDirectory, $StagingDirectory, $ArchivePath)) {
    Assert-ProjectPath $path
}

& (Join-Path $ProjectRoot "build.ps1") -CelesteDir $CelesteDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

[IO.Directory]::CreateDirectory($DistDirectory) | Out-Null
if (Test-Path -LiteralPath $StagingDirectory) {
    Remove-Item -LiteralPath $StagingDirectory -Recurse -Force
}
[IO.Directory]::CreateDirectory($StagingDirectory) | Out-Null

Copy-Item -LiteralPath $EverestYamlPath -Destination $StagingDirectory
Copy-Item -LiteralPath (Join-Path $ProjectRoot "Code") -Destination $StagingDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $ProjectRoot "Loenn") -Destination $StagingDirectory -Recurse

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $ArchivePath) {
    Remove-Item -LiteralPath $ArchivePath -Force
}

$archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $StagingDirectory -Recurse -File) {
        $entryName = $file.FullName.Substring($StagingDirectory.Length + 1).Replace("\", "/")
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $entryName,
            [IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Remove-Item -LiteralPath $StagingDirectory -Recurse -Force

Write-Host "Package complete: $ArchivePath"
