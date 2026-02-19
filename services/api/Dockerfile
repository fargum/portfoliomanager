# Use the official .NET 10 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the .NET 10 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/FtoConsulting.PortfolioManager.Api/FtoConsulting.PortfolioManager.Api.csproj", "src/FtoConsulting.PortfolioManager.Api/"]
COPY ["src/FtoConsulting.PortfolioManager.Application/FtoConsulting.PortfolioManager.Application.csproj", "src/FtoConsulting.PortfolioManager.Application/"]
COPY ["src/FtoConsulting.PortfolioManager.Domain/FtoConsulting.PortfolioManager.Domain.csproj", "src/FtoConsulting.PortfolioManager.Domain/"]
COPY ["src/FtoConsulting.PortfolioManager.Infrastructure/FtoConsulting.PortfolioManager.Infrastructure.csproj", "src/FtoConsulting.PortfolioManager.Infrastructure/"]

RUN dotnet restore "src/FtoConsulting.PortfolioManager.Api/FtoConsulting.PortfolioManager.Api.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/FtoConsulting.PortfolioManager.Api"
RUN dotnet build "FtoConsulting.PortfolioManager.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FtoConsulting.PortfolioManager.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Use the built-in non-root user from the .NET 10 base image
USER app

ENTRYPOINT ["dotnet", "FtoConsulting.PortfolioManager.Api.dll"]