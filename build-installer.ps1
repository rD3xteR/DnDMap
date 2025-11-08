param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

# Очистка
Remove-Item "Releases" -Recurse -ErrorAction SilentlyContinue
Remove-Item "Setup" -Recurse -ErrorAction SilentlyContinue

# Сборка проекта
dotnet clean -c $Configuration
dotnet build -c $Configuration --no-incremental

# Публикация
dotnet publish -c $Configuration -o "publish\$Version" --self-contained false

# Создание пакета для Squirrel
$squirrel = "packages\squirrel.windows\tools\Squirrel.exe"
if (Test-Path $squirrel) {
    & $squirrel --releasify "publish\$Version\DnDMap.dll" --releaseDir "Releases"
    
    # Перемещение установщика
    Move-Item "Releases\Setup.exe" "Setup\DnDMap-Setup.exe" -Force
}