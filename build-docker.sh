#!/bin/bash

# Portfolio Manager API Docker Build Script

echo "Building Portfolio Manager API Docker Image..."

# Build the Docker image
docker build -t portfoliomanager-api:latest ./services/api

if [ $? -eq 0 ]; then
    echo "✅ Docker image built successfully!"
    echo "Image name: portfoliomanager-api:latest"
    echo ""
    echo "To run the container:"
    echo "docker run -p 8080:8080 --name portfoliomanager-api portfoliomanager-api:latest"
    echo ""
    echo "Or use docker-compose:"
    echo "docker-compose up"
else
    echo "❌ Docker build failed!"
    exit 1
fi