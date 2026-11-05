param([Parameter(Mandatory=$true)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
$projectDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$instance = Get-Content -LiteralPath (Join-Path $projectDir 'Library\Pipeline\.unity-pipeline-port') -Raw | ConvertFrom-Json
$headers = @{Authorization=('Bearer ' + $instance.evalToken)}
$payload = [System.IO.File]::ReadAllText([System.IO.Path]::GetFullPath($RequestPath))
$result = Invoke-RestMethod -Method Post -Uri ('http://127.0.0.1:' + $instance.port + '/api/exec') -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($payload)) -TimeoutSec 55
$result | ConvertTo-Json -Depth 18
if ($result.success -eq $false) { exit 1 }

