#Requires -Version 5.1
<#
.SYNOPSIS
    Stages a bundled LibreOffice runtime for the DocumentProcessing PoC without performing a normal Windows install.

.DESCRIPTION
    Verifies the official MSI, runs an MSI "administrative extraction" (msiexec /a), and stages the complete
    LibreOffice runtime without registering the application, writing to the Windows Registry, or modifying PATH.

.PARAMETER MsiPath
    Path to the official LibreOffice Windows x64 MSI installer.

.PARAMETER TargetDirectory
    Directory to extract the administrative install into. Defaults to tools/libreoffice/runtime under this script.

.PARAMETER ExpectedVersion
    Expected executable version. Defaults to the pinned Windows runtime manifest.

.PARAMETER ExpectedSha256
    Expected MSI SHA-256. Defaults to the pinned Windows runtime manifest.

.PARAMETER DestinationDirectory
    One or more runtime roots to stage. Defaults to the Console sample and Worker runtime directories.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,

    [Parameter(Mandatory = $false)]
    [string]$TargetDirectory = (Join-Path $PSScriptRoot "runtime"),

    [Parameter(Mandatory = $false)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $false)]
    [string]$ExpectedSha256,

    [Parameter(Mandatory = $false)]
    [string[]]$DestinationDirectory = @(
        (Join-Path $PSScriptRoot "..\..\samples\DocumentProcessing.Sample.Console\runtimes\libreoffice\win-x64"),
        (Join-Path $PSScriptRoot "..\..\src\DocumentProcessing.Worker\runtimes\libreoffice\win-x64")
    )
)

$ErrorActionPreference = "Stop"
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "runtime-manifest.win-x64.json") -Raw | ConvertFrom-Json
if (-not $ExpectedVersion) { $ExpectedVersion = $manifest.version }
if (-not $ExpectedSha256) { $ExpectedSha256 = $manifest.sha256 }

if (-not (Test-Path -LiteralPath $MsiPath)) {
    throw "MSI not found at path: $MsiPath"
}

$MsiPath = (Resolve-Path -LiteralPath $MsiPath).Path
$TargetDirectory = [System.IO.Path]::GetFullPath($TargetDirectory)

if ($ExpectedSha256) {
    $actualSha256 = (Get-FileHash -LiteralPath $MsiPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $normalizedExpectedSha256 = $ExpectedSha256.Trim().ToLowerInvariant()
    if ($actualSha256 -ne $normalizedExpectedSha256) {
        throw "LibreOffice MSI SHA-256 mismatch. Expected $normalizedExpectedSha256 but found $actualSha256."
    }

    Write-Host "MSI SHA-256 verified: $actualSha256"
}

Write-Host "Extracting LibreOffice MSI (administrative install, no system changes)..."
Write-Host "  MSI:    $MsiPath"
Write-Host "  Target: $TargetDirectory"

New-Item -ItemType Directory -Force -Path $TargetDirectory | Out-Null

$msiArgs = @(
    "/a", "`"$MsiPath`"",
    "TARGETDIR=`"$TargetDirectory`"",
    "/qn"
)

$process = Start-Process -FilePath "msiexec.exe" -ArgumentList $msiArgs -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) {
    throw "msiexec administrative extraction failed with exit code $($process.ExitCode)."
}

$sofficeCandidates = Get-ChildItem -Path $TargetDirectory -Recurse -Filter "soffice.com" -ErrorAction SilentlyContinue
if (-not $sofficeCandidates -or $sofficeCandidates.Count -eq 0) {
    throw "Extraction completed but program/soffice.com was not found under $TargetDirectory. The MSI layout may differ from what this script expects."
}
if (@($sofficeCandidates).Count -ne 1) {
    throw "More than one soffice.com was found. Use a dedicated extraction directory for this version."
}

$programDir = $sofficeCandidates[0].Directory
$runtimeRoot = $programDir.Parent

if ($ExpectedVersion) {
    Write-Host "Checking version (expected: $ExpectedVersion)..."
    $reportedVersion = (& $sofficeCandidates[0].FullName --headless --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "soffice.com --version failed with exit code $LASTEXITCODE."
    }

    if ($reportedVersion -notmatch ("^LibreOffice\s+" + [regex]::Escape($ExpectedVersion) + "(?:\s|$)")) {
        throw "LibreOffice version mismatch. Expected $ExpectedVersion but executable reported: $reportedVersion"
    }

    Write-Host "LibreOffice version verified: $reportedVersion"
}

$profileTemplate = Join-Path $TargetDirectory "profile-template-working"
$profileTemplateArchive = Join-Path $runtimeRoot.FullName "profile-template.zip"
$warmupOutput = Join-Path $TargetDirectory "profile-warmup-output"
$warmupPackage = Join-Path $TargetDirectory "profile-warmup-package"
$warmupDocument = Join-Path $TargetDirectory "profile-warmup.docx"

if ((Test-Path -LiteralPath $profileTemplate) -or (Test-Path -LiteralPath $profileTemplateArchive)) {
    throw "Profile template artifacts already exist under $TargetDirectory. Use a fresh extraction directory."
}

New-Item -ItemType Directory -Force -Path `
    $profileTemplate, `
    $warmupOutput, `
    (Join-Path $warmupPackage "_rels"), `
    (Join-Path $warmupPackage "word") | Out-Null

$contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>
'@
$relationships = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
'@
$documentXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    <w:p><w:r><w:t>Document Processing LibreOffice profile warm-up.</w:t></w:r></w:p>
    <w:sectPr/>
  </w:body>
</w:document>
'@

[IO.File]::WriteAllText((Join-Path $warmupPackage "[Content_Types].xml"), $contentTypes)
[IO.File]::WriteAllText((Join-Path $warmupPackage "_rels\.rels"), $relationships)
[IO.File]::WriteAllText((Join-Path $warmupPackage "word\document.xml"), $documentXml)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($warmupPackage, $warmupDocument)

$profileTemplateUri = [Uri]::new($profileTemplate).AbsoluteUri

Write-Host "Preparing LibreOffice profile template with a real PDF conversion..."
& $sofficeCandidates[0].FullName `
    --headless `
    --nologo `
    --nodefault `
    --norestore `
    --nofirststartwizard `
    "-env:UserInstallation=$profileTemplateUri" `
    --convert-to "pdf:writer_pdf_Export" `
    --outdir $warmupOutput `
    $warmupDocument

if ($LASTEXITCODE -ne 0) {
    throw "LibreOffice profile warm-up failed with exit code $LASTEXITCODE."
}

$warmupPdf = Join-Path $warmupOutput "profile-warmup.pdf"
if (-not (Test-Path -LiteralPath $warmupPdf)) {
    throw "LibreOffice profile warm-up completed without producing $warmupPdf."
}

[IO.Compression.ZipFile]::CreateFromDirectory(
    $profileTemplate,
    $profileTemplateArchive,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

Write-Host "LibreOffice profile template is ready at: $profileTemplateArchive"

foreach ($destination in $DestinationDirectory) {
    $destination = [System.IO.Path]::GetFullPath($destination)
    Write-Host "Staging runtime into $destination ..."
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    # Administrative images include a copy of the installer; it is not a runtime dependency.
    Get-ChildItem -LiteralPath $runtimeRoot.FullName |
        Where-Object { $_.Extension -ne ".msi" } |
        Copy-Item -Destination $destination -Recurse -Force

    $finalSoffice = Join-Path $destination "program\soffice.com"
    if (-not (Test-Path -LiteralPath $finalSoffice)) {
        throw "Staging finished but $finalSoffice does not exist. Aborting."
    }

    Write-Host "Bundled runtime is ready at: $finalSoffice"
}

Write-Host "No LibreOffice installation, registry entry, or PATH change was made on this machine."
