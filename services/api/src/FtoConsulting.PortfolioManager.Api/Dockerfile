FROM mcr.microsoft.com/dotnet/aspnet:9.0-nanoserver-1809 AS base
WORKDIR /app
EXPOSE 5125

ENV ASPNETCORE_URLS=http://+:5125

FROM mcr.microsoft.com/dotnet/sdk:9.0-nanoserver-1809 AS build
ARG configuration=Release
WORKDIR /src
COPY ["services/api/src/FtoConsulting.PortfolioManager.Api/FtoConsulting.PortfolioManager.Api.csproj", "services/api/src/FtoConsulting.PortfolioManager.Api/"]
RUN dotnet restore "services\api\src\FtoConsulting.PortfolioManager.Api\FtoConsulting.PortfolioManager.Api.csproj"
COPY . .
WORKDIR "/src/services/api/src/FtoConsulting.PortfolioManager.Api"
RUN dotnet build "FtoConsulting.PortfolioManager.Api.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "FtoConsulting.PortfolioManager.Api.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FtoConsulting.PortfolioManager.Api.dll"]
