# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ChristinaTicketingSystem.Api/ChristinaTicketingSystem.Api.csproj ChristinaTicketingSystem.Api/
RUN dotnet restore ChristinaTicketingSystem.Api/ChristinaTicketingSystem.Api.csproj

COPY ChristinaTicketingSystem.Api/ ChristinaTicketingSystem.Api/
RUN dotnet publish ChristinaTicketingSystem.Api/ChristinaTicketingSystem.Api.csproj \
    -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Railway injects PORT — ASP.NET Core reads ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "ChristinaTicketingSystem.Api.dll"]
