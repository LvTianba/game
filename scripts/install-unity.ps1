$ErrorActionPreference = 'Stop'
$unityVersion = '6000.6.0f1'
$unityChangeset = 'f7f8ed4d1e24'
$unityInstallRoot = 'D:\unity\editor'
$unityEditor = Join-Path $unityInstallRoot "$unityVersion\Editor\Unity.exe"
$androidPlayer = Join-Path $unityInstallRoot "$unityVersion\Editor\Data\PlaybackEngines\AndroidPlayer"

function Get-UnityHubPath {
    $candidates = @(
        'D:\unity\Unity Hub\Unity Hub.exe',
        "$env:LOCALAPPDATA\Programs\Unity Hub\Unity Hub.exe",
        "$env:LOCALAPPDATA\Unity Hub\Unity Hub.exe",
        'C:\Program Files\Unity Hub\Unity Hub.exe',
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\Unity Hub.exe"
    )

    $appx = Get-AppxPackage -Name 'UnityTechnologies.UnityHub' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $appx) {
        $appx = Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*UnityHub*' } | Select-Object -First 1
    }
    if ($appx) {
        $candidates += (Join-Path $appx.InstallLocation 'app\Unity Hub.exe')
    }

    return ($candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)
}

function Test-AndroidModulesPresent {
    $ndk = Join-Path $androidPlayer 'NDK'
    $sdk = Join-Path $androidPlayer 'SDK'
    $openJdk = Join-Path $androidPlayer 'OpenJDK'

    $present = (Test-Path -LiteralPath $androidPlayer) -and
        (Test-Path -LiteralPath (Join-Path $ndk 'build')) -and
        (Test-Path -LiteralPath (Join-Path $ndk 'toolchains')) -and
        (Test-Path -LiteralPath (Join-Path $sdk 'platform-tools')) -and
        (Test-Path -LiteralPath (Join-Path $sdk 'cmdline-tools')) -and
        (Test-Path -LiteralPath (Join-Path $sdk 'platforms')) -and
        (Test-Path -LiteralPath (Join-Path $openJdk 'bin'))

    return $present
}

$hub = Get-UnityHubPath
if (-not $hub) {
    winget install --id Unity.UnityHub --exact --silent `
        --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget install Unity Hub failed with exit code $LASTEXITCODE"
    }
    $hub = Get-UnityHubPath
}
if (-not $hub) { throw 'Unity Hub executable was not found.' }

function Invoke-UnityHubHeadless {
    param([string[]]$Arguments)

    $out = Join-Path $env:TEMP ("unity-hub-{0}-out.txt" -f [guid]::NewGuid().ToString('N'))
    $err = Join-Path $env:TEMP ("unity-hub-{0}-err.txt" -f [guid]::NewGuid().ToString('N'))
    Remove-Item -LiteralPath $out,$err -ErrorAction SilentlyContinue

    $p = Start-Process -FilePath $hub -ArgumentList $Arguments -Wait -PassThru `
        -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err

    Get-Content -LiteralPath $out -ErrorAction SilentlyContinue | Out-Host
    Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Out-Host

    $global:LASTEXITCODE = $p.ExitCode
    if ($LASTEXITCODE -ne 0) {
        throw "Unity Hub command failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

Invoke-UnityHubHeadless -Arguments @(
    '--','--headless','install-path','--set',$unityInstallRoot
)

if (-not (Test-Path -LiteralPath $unityEditor)) {
    Invoke-UnityHubHeadless -Arguments @(
        '--','--headless','install',
        '--version',$unityVersion,
        '--changeset',$unityChangeset,
        '--module','android','android-sdk-ndk-tools','android-open-jdk-17.0.18+8'
    )
}
elseif (-not (Test-AndroidModulesPresent)) {
    Invoke-UnityHubHeadless -Arguments @(
        '--','--headless','install-modules',
        '--version',$unityVersion,
        '--module','android','android-sdk-ndk-tools','android-open-jdk-17.0.18+8'
    )
}
else {
    Write-Output 'Android modules are already installed; skipping Unity Hub install-modules.'
}