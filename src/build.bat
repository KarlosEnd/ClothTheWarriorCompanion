@echo off
dotnet build -c Release

if %ERRORLEVEL% EQU 0 (
    echo Compilacion exitosa. Cerrando en 3 segundos...
    timeout /t 1 >nul
) else (
    echo Error de compilacion. Presiona cualquier tecla para cerrar.
    pause >nul
)