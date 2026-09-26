# ==============================================================================
# Script: sync_models.ps1
# Dong bo toan bo Yes Steve Model tu YSM_Models_Pool vao:
# 1. testip.json (danh sach skin cua Launcher)
# 2. remote_config_cache.json (cache offline)
# 3. ReleaseApp\models_pool\
# 4. CobblemonInstaller\payload.zip (nhung vao bo cai dat installer)
# 5. Dong goi lai CobblemonLauncher va CobblemonInstaller
# ==============================================================================

param(
    [switch]$RebuildOnly = $false
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$poolDir = Join-Path $rootDir "YSM_Models_Pool"
$testIpJson = Join-Path $rootDir "testip.json"
$cacheJson = Join-Path $rootDir "CobblemonLauncher\remote_config_cache.json"
$installerDir = Join-Path $rootDir "CobblemonInstaller"
$payloadZip = Join-Path $installerDir "payload.zip"
$releaseAppDir = Join-Path $rootDir "ReleaseApp"
$installerPublishDir = Join-Path $rootDir "publish_installer"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "          DONG BO HE THONG MODEL YES STEVE MODEL          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Test-Path $poolDir)) {
    New-Item -ItemType Directory -Path $poolDir -Force | Out-Null
}

# 1. Giai nen tat ca file .zip trong YSM_Models_Pool neu co
$zipFiles = Get-ChildItem -Path $poolDir -Filter "*.zip" -File
foreach ($zip in $zipFiles) {
    Write-Host "[ZIP] Dang giai nen: $($zip.Name)..." -ForegroundColor Yellow
    $tempExtract = Join-Path $poolDir ("_extracted_" + $zip.BaseName)
    try {
        Expand-Archive -Path $zip.FullName -DestinationPath $tempExtract -Force
        # Tim cac file .ysm ben trong
        Get-ChildItem -Path $tempExtract -Recurse -Filter "*.ysm" | ForEach-Object {
            $destPath = Join-Path $poolDir $_.Name
            Move-Item -Path $_.FullName -Destination $destPath -Force
            Write-Host "  -> Tim thay model: $($_.Name)" -ForegroundColor Green
        }
        Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Warning "Khong the giai nen $($zip.Name): $_"
    }
}

# 2. Quet tat ca file .ysm trong YSM_Models_Pool
$ysmFiles = Get-ChildItem -Path $poolDir -Filter "*.ysm" -File
Write-Host "[POOL] Tim thay $($ysmFiles.Count) model .ysm trong YSM_Models_Pool" -ForegroundColor Cyan

# 3. Cap nhat testip.json va remote_config_cache.json
if (Test-Path $testIpJson) {
    $jsonContent = Get-Content -Path $testIpJson -Raw -Encoding UTF8 | ConvertFrom-Json
    $skinsList = [System.Collections.Generic.List[PSCustomObject]]::new()

    foreach ($file in $ysmFiles) {
        $baseName = $file.BaseName
        $safeId = ($baseName -replace "[^a-zA-Z0-9_]", "_").ToLower()
        $friendlyName = $baseName

        if ($friendlyName -match "^wine_fox_\d+_(.*)$") {
            $sub = $matches[1] -replace "_", " "
            $friendlyName = "Wine Fox " + (Get-Culture).TextInfo.ToTitleCase($sub.ToLower())
        }
        elseif ($friendlyName -match "^misc_\d+_(.*)$") {
            $sub = $matches[1] -replace "_", " "
            $friendlyName = (Get-Culture).TextInfo.ToTitleCase($sub.ToLower())
        }
        else {
            $friendlyName = $friendlyName -replace "_", " " -replace "GI ", "Genshin " -replace "v1", "" -replace "v2\.1", ""
            $friendlyName = (Get-Culture).TextInfo.ToTitleCase($friendlyName.ToLower())
        }
        $friendlyName = $friendlyName.Trim()

        $skinObj = [PSCustomObject]@{
            id = $safeId
            name = $friendlyName
            description = "Custom Yes Steve Model danh rieng cho Da Cuoi Mon"
            price = 1000
            fileName = $file.Name
            downloadUrl = ""
        }
        $skinsList.Add($skinObj)
    }

    $jsonContent.skins = $skinsList
    $newJsonStr = $jsonContent | ConvertTo-Json -Depth 10
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($testIpJson, $newJsonStr, $utf8NoBom)
    [System.IO.File]::WriteAllText((Join-Path $rootDir "testip"), $newJsonStr, $utf8NoBom)
    [System.IO.File]::WriteAllText($cacheJson, $newJsonStr, $utf8NoBom)
    Write-Host "[JSON] Da cap nhat $($skinsList.Count) skin vao testip, testip.json va remote_config_cache.json" -ForegroundColor Green
}

# 3.5 Render anh thumbnail 2D cho cac model .ysm bang Windows Thumbnail Provider engine (chi render file con thieu)
Write-Host "[THUMBNAIL] Dang render thumbnail 2D cho tat ca model trong pool..." -ForegroundColor Yellow
$thumbToolProj = Join-Path $rootDir "Tools\YsmThumbnailGen\YsmThumbnailGen.csproj"
dotnet run --project $thumbToolProj -c Release -- --dir $poolDir --size 256

# Xoa module 3D YSMViewer thua trong ReleaseApp neu con sot lai tu truoc
$staleViewer = Join-Path $releaseAppDir "YSMViewer"
if (Test-Path $staleViewer) {
    Remove-Item -Path $staleViewer -Recurse -Force -ErrorAction SilentlyContinue
}

# 4. Copy vao ReleaseApp\models_pool\
$releasePool = Join-Path $releaseAppDir "models_pool"
if (-not (Test-Path $releasePool)) {
    New-Item -ItemType Directory -Path $releasePool -Force | Out-Null
}
foreach ($file in $ysmFiles) {
    Copy-Item -Path $file.FullName -Destination (Join-Path $releasePool $file.Name) -Force
}
$pngFiles = Get-ChildItem -Path $poolDir -Filter "*.png" -File
foreach ($file in $pngFiles) {
    Copy-Item -Path $file.FullName -Destination (Join-Path $releasePool $file.Name) -Force
}
Write-Host "[COPY] Da copy toan bo model ($($ysmFiles.Count) ysm, $($pngFiles.Count) png) vao ReleaseApp\models_pool\" -ForegroundColor Green

# 5. Cap nhat payload.zip (bao gom ca Launcher va models_pool)
Write-Host "[BUILD] Dang bien dich CobblemonLauncher v2.0.1 (toi uu nen SingleFile)..." -ForegroundColor Yellow
Stop-Process -Name CobblemonLauncher -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
dotnet publish "$rootDir\CobblemonLauncher\CobblemonLauncher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $releaseAppDir

# Tinh ma SHA-256 cho ban cap nhat v2.0.1
$launcherExePath = Join-Path $releaseAppDir "CobblemonLauncher.exe"
$launcherSha256 = (Get-FileHash -Path $launcherExePath -Algorithm SHA256).Hash.ToLower()
Write-Host "[SHA256] Ma SHA-256 cua CobblemonLauncher.exe: $launcherSha256" -ForegroundColor Cyan

# Cap nhat metadata v2.0.1 vao testip va testip.json
if (Test-Path $testIpJson) {
    $jsonContent = Get-Content -Path $testIpJson -Raw -Encoding UTF8 | ConvertFrom-Json
    $jsonContent.launcher_version = "2.0.1"
    $jsonContent.launcher_download_url = "https://github.com/VVKT0905/DaCuoiMon-Launcher/releases/latest/download/CobblemonLauncher.exe"
    $jsonContent | Add-Member -MemberType NoteProperty -Name "launcher_sha256" -Value $launcherSha256 -Force
    $jsonContent | Add-Member -MemberType NoteProperty -Name "launcher_changelog" -Value @(
        "Bổ sung thư viện Architectury API (v13.0.11) hỗ trợ hoàn hảo Mega Showdown & Navas ZA Megas.",
        "Nâng cấp công nghệ Atomic In-Place Swap (Cập nhật tức thì, không giật lag).",
        "Tích hợp xác thực mã băm SHA-256 chống lỗi file đường truyền.",
        "Giao diện HUD tải cập nhật cao cấp theo thời gian thực (MB/s)."
    ) -Force

    $newJsonStr = $jsonContent | ConvertTo-Json -Depth 10
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($testIpJson, $newJsonStr, $utf8NoBom)
    [System.IO.File]::WriteAllText((Join-Path $rootDir "testip"), $newJsonStr, $utf8NoBom)
    [System.IO.File]::WriteAllText($cacheJson, $newJsonStr, $utf8NoBom)
    Write-Host "[JSON] Da cap nhat version 2.0.1 va SHA-256 vao testip va testip.json" -ForegroundColor Green
}

$tempPayloadDir = Join-Path $rootDir "temp_payload_build"
if (Test-Path $tempPayloadDir) {
    Remove-Item -Path $tempPayloadDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempPayloadDir -Force | Out-Null

# Copy Assets, Launcher.exe & testip.json
Copy-Item -Path "$rootDir\CobblemonLauncher\Assets" -Destination (Join-Path $tempPayloadDir "Assets") -Recurse -Force
Copy-Item -Path "$releaseAppDir\CobblemonLauncher.exe" -Destination (Join-Path $tempPayloadDir "CobblemonLauncher.exe") -Force
Copy-Item -Path $testIpJson -Destination (Join-Path $releaseAppDir "testip.json") -Force
Copy-Item -Path $testIpJson -Destination (Join-Path $tempPayloadDir "testip.json") -Force

# Copy models_pool vao payload
$payloadModelsPool = Join-Path $tempPayloadDir "models_pool"
New-Item -ItemType Directory -Path $payloadModelsPool -Force | Out-Null
foreach ($file in $ysmFiles) {
    Copy-Item -Path $file.FullName -Destination (Join-Path $payloadModelsPool $file.Name) -Force
}
foreach ($file in $pngFiles) {
    Copy-Item -Path $file.FullName -Destination (Join-Path $payloadModelsPool $file.Name) -Force
}

# Tao payload.zip moi
if (Test-Path $payloadZip) {
    Remove-Item -Path $payloadZip -Force
}
Compress-Archive -Path "$tempPayloadDir\*" -DestinationPath $payloadZip -CompressionLevel Optimal
Remove-Item -Path $tempPayloadDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "[ZIP] Da dong goi payload.zip chua Launcher + models_pool thanh cong!" -ForegroundColor Green

# 6. Bien dich lai CobblemonInstaller
Write-Host "[BUILD] Dang bien dich CobblemonInstaller (nhung payload.zip moi, toi uu nen)..." -ForegroundColor Yellow
dotnet publish "$rootDir\CobblemonInstaller\CobblemonInstaller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $installerPublishDir

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " [THANH CONG] Toan bo he thong da duoc dong bo!" -ForegroundColor Green
Write-Host " 1. Folder Server: $poolDir" -ForegroundColor Green
Write-Host " 2. Bo cai dat: $installerPublishDir\CobblemonInstaller.exe" -ForegroundColor Green
Write-Host " 3. Launcher test: $releaseAppDir\CobblemonLauncher.exe" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
