$ErrorActionPreference = "Continue"

Write-Host "`n>>> Đang dừng các instance VietTravel cũ..." -ForegroundColor Cyan
# Kill triệt để cả process tree
taskkill /F /IM "VietTravel.UI.exe" /T 2>$null | Out-Null
Stop-Process -Name "VietTravel.UI" -Force -ErrorAction SilentlyContinue

Write-Host ">>> Đang build..." -ForegroundColor Cyan
dotnet build -nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[!] Build thất bại." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ">>> Đang khởi chạy VietTravel (Nhấn Ctrl+C để dừng)..." -ForegroundColor Green
$exePath = "VietTravel.UI\bin\Debug\net10.0-windows\VietTravel.UI.exe"

try {
    if (Test-Path $exePath) {
        # Chạy trực tiếp exe và chờ nó kết thúc để Ctrl+C có tác dụng
        Write-Host ">>> Khởi chạy: $exePath" -ForegroundColor Gray
        $proc = Start-Process -FilePath $exePath -PassThru
        $proc | Wait-Process
    }
    else {
        Write-Host ">>> Không tìm thấy exe, chạy bằng dotnet run..." -ForegroundColor Yellow
        dotnet run --project VietTravel.UI --no-build
    }
}
finally {
    # Khi nhấn Ctrl+C hoặc thoát script, đảm bảo app cũng bị kill
    Write-Host "`n>>> Đang dọn dẹp task..." -ForegroundColor Yellow
    taskkill /F /IM "VietTravel.UI.exe" /T 2>$null | Out-Null
    Stop-Process -Name "VietTravel.UI" -Force -ErrorAction SilentlyContinue
}
