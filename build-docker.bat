@echo off
REM Portfolio Manager API Docker Build Script for Windows

echo Building Portfolio Manager API Docker Image...

REM Build the Docker image
docker build -t portfoliomanager-api:latest .

if %ERRORLEVEL% equ 0 (
    echo ✅ Docker image built successfully!
    echo Image name: portfoliomanager-api:latest
    echo.
    echo To run the container:
    echo docker run -p 8080:8080 --name portfoliomanager-api portfoliomanager-api:latest
    echo.
    echo Or use docker-compose:
    echo docker-compose up
) else (
    echo ❌ Docker build failed!
    exit /b 1
)