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

$appUri = "https://github.com/$username/$repo/releases/latest/download/$appZipName"
Invoke-WebRequest -Uri $appUri -OutFile $appZipPath

Expand-Archive -LiteralPath $appZipPath -DestinationPath $tempPath -Force

& $appExecPath update

$hvcc_HOME = '{{$homepath}}'

if ($env:PATH -notlike "*$hvcc_HOME*") {
    $env:PATH = $env:PATH + ";$hvcc_HOME\;$hvcc_HOME";
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\' -Name Path -Value $env:PATH
}
