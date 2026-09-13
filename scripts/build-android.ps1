$ErrorActionPreference = 'Stop'
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
$project = Join-Path $PSScriptRoot '..\BorderValley'
$output = Join-Path $PSScriptRoot '..\Builds\Android\BorderValley.apk'
New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null

& $unity -batchmode -quit -nographics `
    -projectPath $project `
    -executeMethod BorderValley.Editor.Build.BuildAndroid.PerformBuild `
    -logFile (Join-Path $PSScriptRoot '..\android-build.log') | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Android build failed: $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $output)) { throw "APK not found: $output" }
Write-Output "Built $output"
