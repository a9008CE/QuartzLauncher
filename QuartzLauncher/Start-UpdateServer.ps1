$ErrorActionPreference = "Stop"

$exe = Join-Path $PSScriptRoot "dist\QuartzLauncher.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "未找到 $exe，请先通过 dotnet publish 生成 dist 后再运行本脚本。"
}

$conflicts = Get-NetTCPConnection -LocalPort 47831 -State Listen -ErrorAction SilentlyContinue
foreach ($connection in $conflicts) {
    try {
        Stop-Process -Id $connection.OwningProcess -Force -ErrorAction SilentlyContinue
    }
    catch { }
}

Start-Process -FilePath $exe `
    -ArgumentList "--update-server", "--listen", "any", "--port", "47831" `
    -WorkingDirectory (Split-Path -Parent $exe) `
    -WindowStyle Hidden

Write-Host "更新服务已启动：http://192.168.1.3:47831/update.json ($exe)"