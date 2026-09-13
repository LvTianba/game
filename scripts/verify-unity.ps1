$ErrorActionPreference = 'Stop'
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) {
    throw "Unity 6000.6.0f1 is not installed at $unity"
}

$androidPlayer = 'D:\unity\editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer'
if (-not (Test-Path -LiteralPath $androidPlayer)) {
    throw "Android Playback Engine is not installed at $androidPlayer"
}

$expectedProductVersion = '6000.6.0f1_f7f8ed4d1e24'
$unityInfo = (Get-Item -LiteralPath $unity).VersionInfo
if ($unityInfo.ProductVersion -ne $expectedProductVersion) {
    throw "Unexpected Unity product version: $($unityInfo.ProductVersion) (expected $expectedProductVersion)"
}

$versionOut = Join-Path $env:TEMP 'unity-6000.6.0f1-version-out.txt'
$versionErr = Join-Path $env:TEMP 'unity-6000.6.0f1-version-err.txt'
Remove-Item -LiteralPath $versionOut,$versionErr -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList @('-version') -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $versionOut -RedirectStandardError $versionErr
if ($p.ExitCode -ne 0) {
    throw "Unity -version failed with exit code $($p.ExitCode)"
}
$version = (Get-Content -LiteralPath $versionOut -Raw -ErrorAction SilentlyContinue).Trim()
if ($version -notmatch '6000\.6\.0f1') {
    throw "Unexpected Unity version: $version"
}

$batchOut = Join-Path $env:TEMP 'unity-6000.6.0f1-batchmode-out.txt'
$batchErr = Join-Path $env:TEMP 'unity-6000.6.0f1-batchmode-err.txt'
Remove-Item -LiteralPath $batchOut,$batchErr -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-quit','-nographics','-logFile','-') -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $batchOut -RedirectStandardError $batchErr
Get-Content -LiteralPath $batchOut -ErrorAction SilentlyContinue | Out-Host
Get-Content -LiteralPath $batchErr -ErrorAction SilentlyContinue | Out-Host
if ($p.ExitCode -ne 0) {
    throw "Unity batch mode failed with exit code $($p.ExitCode)"
}
Write-Output 'Unity 6000.6.0f1 toolchain is ready.'