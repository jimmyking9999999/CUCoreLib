param(
    [string]$GamePath = "F:/SteamLibrary/steamapps/common/Casualties Unknown Demo",
    [string]$ModName = "CUCoreLib",
    [string]$ModNameSpace = "CUCoreLib"
)

$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
chcp 65001 > $null

$timestamp = Get-Date -Format "yyyy-MM-dd_HH.mm.ss"

$GamePath = [System.IO.Path]::GetFullPath($GamePath)
$bepInExPath = [System.IO.Path]::Combine($GamePath, "BepInEx")

$GameLog = Join-Path $env:USERPROFILE "AppData\LocalLow\Orsoniks\CasualtiesUnknown\Player.log"
$GameExecutable = [System.IO.Path]::Combine($GamePath, "CasualtiesUnknown.exe")
$ModDll = [System.IO.Path]::Combine($PSScriptRoot, "bin/Debug/", "$ModNameSpace.dll")

$logDestination = [System.IO.Path]::Combine($PSScriptRoot, "Logs", "$timestamp.log")

if (-not (Test-Path $GamePath -PathType Container))
{
    Write-Error "游戏路径无效或不是目录: $GamePath"
    exit 1
}

$logsFolder = [System.IO.Path]::Combine($PSScriptRoot, "Logs")
if (-not (Test-Path $logsFolder))
{
    New-Item -ItemType Directory -Path $logsFolder -Force
}

function Write-ColoredMessage
{
    param (
        [string]$Message,
        [System.ConsoleColor]$Color
    )
    Write-Host $Message -ForegroundColor $Color
}

function Copy-BepInExLog
{
    if (Test-Path $GameLog)
    {
        try
        {
            Copy-Item $GameLog $logDestination -Force
            Write-ColoredMessage "正在复制 BepInEx 日志到 ""$logDestination""." Cyan
        }
        catch
        {
            Write-Warning "复制 BepInEx 日志失败: $_"
        }
    }
}

function Interval
{
    Write-Host "----------------------------------------"
}

if (Test-Path $GameLog)
{
    Clear-Content $GameLog
    Write-ColoredMessage "已清空之前的日志文件。" Cyan
}

Write-ColoredMessage "游戏路径: $GamePath" Yellow
Write-ColoredMessage "模组名称: $ModName" Yellow
Write-ColoredMessage "命名空间: $ModNameSpace" Yellow

try
{
    $pluginPath = [System.IO.Path]::Combine($bepInExPath, "plugins")
    New-Item -ItemType Directory -Path $pluginPath -Force
    Copy-Item $ModDll ([System.IO.Path]::Combine($pluginPath, "$ModNameSpace.dll")) -Force
    Write-ColoredMessage "正在复制模组 DLL 到 ""$pluginPath\$ModNameSpace.dll""." Cyan
}
catch
{
    Write-Error "复制模组 DLL 失败: $_"
    exit 1
}

try
{
    $destDocPath = [System.IO.Path]::Combine($bepInExPath, "plugins")
    $copiedDocs = 0
    
    if ($copiedDocs -gt 0)
    {
        Write-ColoredMessage "已成功复制 $copiedDocs 个文档文件到插件目录。" Green
    }
}
catch
{
    Write-Warning "复制文档文件失败: $_"
}

try
{
    $gameProcess = Start-Process -FilePath $GameExecutable `
        -WorkingDirectory (Split-Path $GameExecutable -Parent) `
        -PassThru -NoNewWindow

    Write-ColoredMessage "游戏进程已启动, PID: $( $gameProcess.Id )" Yellow
    Interval

    $lastReadPosition = 0
    while (!$gameProcess.HasExited)
    {
        if (Test-Path $GameLog)
        {
            $content = Get-Content $GameLog -ReadCount 0 -Encoding UTF8
            for ($i = $lastReadPosition; $i -lt $content.Count; $i++) {
                $line = $content[$i]
                $color = "White"
                if ($line -match "^\[Error")
                {
                    $color = "Red"
                }
                elseif ($line -match "^\[Warning")
                {
                    $color = "Yellow"
                }
                elseif ($line -match "^\[Info")
                {
                    $color = "White"
                }
                elseif ($line -match "^\[Message")
                {
                    $color = "Blue"
                }
                elseif ($line -match "^\[Debug")
                {
                    $color = "Magenta"
                }
                Write-ColoredMessage $line $color
            }
            $lastReadPosition = $content.Count
        }
        Start-Sleep -Milliseconds 500
    }

    Interval
    Write-ColoredMessage "游戏进程已退出。" Red
}

catch
{
    Write-Error "启动游戏进程失败: $_"
    exit 1
}

finally
{
    if ($gameProcess -and !$gameProcess.HasExited)
    {
        Interval
        Write-ColoredMessage "正在终止游戏进程..." Red
        $gameProcess.Kill()
    }
    Copy-BepInExLog
}
