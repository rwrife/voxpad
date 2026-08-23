param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string] $RuntimeIdentifier
)

$ErrorActionPreference = "Stop"
$publishPath = (Resolve-Path $PublishDirectory).Path
$executablePath = Join-Path $publishPath "Voxpad.Desktop.exe"

if (-not (Test-Path -Path $executablePath -PathType Leaf)) {
    throw "Published desktop executable was not found: $executablePath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputPath = Join-Path $OutputDirectory "voxpad-$RuntimeIdentifier.zip"

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $outputPath -CompressionLevel Optimal

if ((Get-Item $outputPath).Length -le 0) {
    throw "The packaged ZIP is empty: $outputPath"
}

Write-Host "PACKAGED_WINDOWS=$outputPath"
