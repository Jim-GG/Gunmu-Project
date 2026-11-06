param([string]$Command,[string]$Parameters='{}')
$ErrorActionPreference='Stop'
$projectDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$instance=Get-Content -LiteralPath (Join-Path $projectDir 'Library/Pipeline/.unity-pipeline-port') -Raw|ConvertFrom-Json
$payload=@{command=$Command;parameters=($Parameters|ConvertFrom-Json)}|ConvertTo-Json -Depth 30 -Compress
Invoke-RestMethod -Method Post -Uri ('http://127.0.0.1:'+$instance.port+'/api/exec') -Headers @{Authorization=('Bearer '+$instance.evalToken)} -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) -TimeoutSec 90|ConvertTo-Json -Depth 30
