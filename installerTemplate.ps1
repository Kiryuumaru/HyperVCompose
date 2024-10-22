$username = '{{$username}}'
$repo = '{{$repo}}'
$appname = '{{$appname}}'
$appexec = '{{$appexec}}'
$rootextract = '{{$rootextract}}'

$root = "$env:TEMP\$username\$repo\$appname"
$tempPath = "$root\temp"

$ErrorActionPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Path "$tempPath" -ErrorAction SilentlyContinue | Out-Null
$ErrorActionPreference = 'Stop'

$appZipName = "$appname.zip"
$appPath = "$tempPath\$appname"
$appZipPath = "$tempPath\$appZipName"
$appExecPath = "$appPath\$appexec"

if (Test-Path $appZipPath) {
    Remove-Item -Path $appZipPath -Force
}

$appUri = "https://github.com/$repo/releases/latest/download/$appZipName"
Invoke-WebRequest -Uri $appUri -OutFile $appZipPath

Expand-Archive -LiteralPath $appZipPath -DestinationPath $tempPath -Force

& $appExecPath update
