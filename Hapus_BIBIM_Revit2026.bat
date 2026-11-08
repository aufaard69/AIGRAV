@echo off
echo ===================================================
echo   PENGHAPUS ADD-IN BIBIM AI DARI REVIT 2026
echo ===================================================
echo.

del /f /q "C:\ProgramData\Autodesk\Revit\Addins\2026\Bibim.Core.addin" 2>nul

if exist "C:\ProgramData\Autodesk\Revit\Addins\2026\Bibim.Core.addin" (
    echo [INFO] Membutuhkan hak akses Administrator.
    echo Silakan klik kanan file ini lalu pilih "Run as administrator".
) else (
    echo [BERHASIL] File manifest BIBIM AI telah dihapus dari Revit 2026!
    echo Pada pembukaan Revit 2026 berikutnya, BIBIM AI tidak akan dimuat lagi.
)

echo.
pause
