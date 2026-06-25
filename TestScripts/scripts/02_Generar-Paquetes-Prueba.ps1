param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

Write-HHStep "02 - Generación de paquetes de prueba"

New-Item -ItemType Directory -Force -Path $config.PackagesRoot | Out-Null

function New-HHZipPackage {
    param(
        [string]$PackageName,
        [string]$CenterId = "1234",
        [string]$Flow = "Download",
        [ValidateSet(
            "Valid",
            "InvalidHash",
            "NoXml",
            "TwoXml",
            "MissingDeclaredFile",
            "ExtraUndeclaredFile",
            "SizeMismatch",
            "InvalidHashAlgorithm",
            "MalformedXml",
            "UnknownCenter"
        )]
        [string]$Mode
    )

    $work = Join-Path $config.PackagesRoot ("work_" + [IO.Path]::GetFileNameWithoutExtension($PackageName))
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    $payloadName = "documento1.txt"
    $payloadPath = Join-Path $work $payloadName
    $payloadText = "Contenido de prueba HeimdallHash - $PackageName - $(Get-Date -Format o)"
    [IO.File]::WriteAllText($payloadPath, $payloadText, [Text.UTF8Encoding]::new($false))

    $payloadBytes = [IO.File]::ReadAllBytes($payloadPath)
    $payloadSize = $payloadBytes.Length
    $payloadHash = (Get-FileHash -Path $payloadPath -Algorithm SHA256).Hash
    $hashAlgorithm = "SHA256"

    if ($Mode -eq "InvalidHash") {
        $payloadHash = "0000000000000000000000000000000000000000000000000000000000000000"
    }

    if ($Mode -eq "SizeMismatch") {
        $payloadSize = $payloadSize + 100
    }

    if ($Mode -eq "InvalidHashAlgorithm") {
        $hashAlgorithm = "BLAKE3"
    }

    if ($Mode -eq "UnknownCenter") {
        $CenterId = "9999"
    }

    $xml = @"
<DeliveryNote>
  <DestinationCenterId>$CenterId</DestinationCenterId>
  <Files>
    <File>
      <Name>$payloadName</Name>
      <OriginalName>$payloadName</OriginalName>
      <Format>txt</Format>
      <Size>$payloadSize</Size>
      <HashAlgorithm>$hashAlgorithm</HashAlgorithm>
      <Hash>$payloadHash</Hash>
    </File>
  </Files>
</DeliveryNote>
"@

    if ($Mode -eq "MalformedXml") {
        $xml = "<DeliveryNote><DestinationCenterId>1234</DestinationCenterId><Files><File><Name>documento1.txt</Name>"
    }

    if ($Mode -ne "NoXml") {
        [IO.File]::WriteAllText((Join-Path $work "DeliveryNote.xml"), $xml, [Text.UTF8Encoding]::new($false))
    }

    if ($Mode -eq "TwoXml") {
        [IO.File]::WriteAllText((Join-Path $work "DeliveryNote_2.xml"), $xml, [Text.UTF8Encoding]::new($false))
    }

    if ($Mode -eq "MissingDeclaredFile") {
        Remove-Item $payloadPath -Force -ErrorAction SilentlyContinue
    }

    if ($Mode -eq "ExtraUndeclaredFile") {
        [IO.File]::WriteAllText((Join-Path $work "fichero_extra_no_declarado.txt"), "Fichero extra no declarado en la DN.", [Text.UTF8Encoding]::new($false))
    }

    $zipPath = Join-Path $config.PackagesRoot $PackageName
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

    Compress-Archive -Path (Join-Path $work "*") -DestinationPath $zipPath -Force
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

    Write-HHOk "Paquete generado: $zipPath"
}

New-HHZipPackage -PackageName "HH_VALID_DN_1234_Download.zip" -Mode Valid
New-HHZipPackage -PackageName "HH_INVALID_HASH_1234_Download.zip" -Mode InvalidHash
New-HHZipPackage -PackageName "HH_INVALID_NO_XML.zip" -Mode NoXml
New-HHZipPackage -PackageName "HH_INVALID_TWO_XML.zip" -Mode TwoXml
New-HHZipPackage -PackageName "HH_VALID_PENDING_1234_Download.zip" -Mode Valid

New-HHZipPackage -PackageName "HH_INVALID_DECLARED_FILE_MISSING.zip" -Mode MissingDeclaredFile
New-HHZipPackage -PackageName "HH_INVALID_EXTRA_FILE.zip" -Mode ExtraUndeclaredFile
New-HHZipPackage -PackageName "HH_INVALID_SIZE_MISMATCH.zip" -Mode SizeMismatch
New-HHZipPackage -PackageName "HH_INVALID_HASH_ALGORITHM.zip" -Mode InvalidHashAlgorithm
New-HHZipPackage -PackageName "HH_INVALID_MALFORMED_XML.zip" -Mode MalformedXml
New-HHZipPackage -PackageName "HH_INVALID_UNKNOWN_CENTER_9999.zip" -Mode UnknownCenter

Write-HHOk "Paquetes de prueba generados correctamente."
