$ErrorActionPreference = 'Stop'
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
$project = Join-Path $PSScriptRoot '..\BorderValley'

foreach ($platform in @('EditMode', 'PlayMode')) {
    $results = Join-Path $PSScriptRoot "..\$($platform.ToLower())-results.xml"
    & $unity -batchmode -nographics -projectPath $project `
        -runTests -testPlatform $platform `
        -testResults $results `
        -logFile (Join-Path $PSScriptRoot "..\$($platform.ToLower())-tests.log") | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$platform tests failed: $LASTEXITCODE" }
}
Write-Output 'All Unity tests passed.'
