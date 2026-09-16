# 局域网更新服务

本机（LAN IP `192.168.1.3`）是固定的根更新服务器。客户端默认更新清单地址为：

```
http://192.168.1.3:47831/update.json
```

## 服务器启动方式

服务器会自动随启动器启动，无需手动操作：

- **自动更新模式**：每次启动启动器，自动检查 47831 端口，若未监听则自动拉起
  `--update-server --listen any --port 47831` 子进程，然后检查更新；有更新会弹窗提示。
- **手动更新模式**：在设置页点击“获取更新”按钮，同样会自动确保服务器在运行后再检查。

若服务器未运行且端口被占用导致无法启动，设置页会给出错误提示。

也可手动启动服务器（会自动停止占用 47831 端口的进程，然后从 `dist` 启动）：

```powershell
.\Start-UpdateServer.ps1
```

手动命令：

```powershell
.\QuartzLauncher.exe --update-server --listen any --port 47831
```

`--listen any` 会监听所有接口，服务日志 `Launcher\updates\server.log` 会列出可供客户端使用的活动 IPv4 地址。客户端使用本机的固定地址 `http://192.168.1.3:47831/update.json`，不能填写 `127.0.0.1`。

## 发布新版本

1. 将版本号升到更高（如 `0.1.5`），执行 `dotnet publish -c Release -o dist`。
2. 将新发布的 `dist\QuartzLauncher.exe` 压缩为 `Launcher\updates\QuartzLauncher-<版本>.zip`。
3. 计算 ZIP 的 SHA-256，更新 `Launcher\updates\update.json` 的 `Version`、`PackageUrl`、`Sha256`、`Notes`。
4. 客户端在自动/手动检查更新时会自动发现并弹窗。

## 更新清单示例

清单中的 `PackageUrl` 可以相对于清单地址：

```json
{
  "Version": "0.1.4",
  "PackageUrl": "QuartzLauncher-0.1.4.zip",
  "Sha256": "...",
  "Notes": "更新说明"
}
```

相对地址会解析到清单所在目录，即 `http://192.168.1.3:47831/QuartzLauncher-0.1.4.zip`，补丁包与 `update.json` 一起放在 `Launcher\updates` 下即可被客户端下载。

## 首次运行准备

首次使用非回环地址时，以管理员身份运行 PowerShell 并添加 URL ACL。监听 `any` 时使用 `+` 前缀：

```powershell
netsh http add urlacl url=http://+:47831/ user="$env:USERDOMAIN\$env:USERNAME"
```

仅为专用网络和本地子网开放防火墙端口。若服务器位于域网络（如 `cangkong.tech`），请使用 `profile=any` 以便域/公用网络下的客户端也能访问：

```powershell
New-NetFirewallRule -DisplayName "QuartzLauncher Update Server" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 47831 -Profile Any -RemoteAddress LocalSubnet
```

已按旧命令创建、但仅覆盖专用网络的规则，可扩为所有配置文件：

```powershell
netsh advfirewall firewall set rule name="QuartzLauncher Update Server" new profile=any
```

旧的回环用法和位置端口仍可使用：`QuartzLauncher.exe --update-server 47831`。
