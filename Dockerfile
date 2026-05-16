# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching for restore)
COPY MorkosiaPrepaLeague.sln ./
COPY MorkosiaPrepaLeague/MorkosiaPrepaLeague.csproj MorkosiaPrepaLeague/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build and publish in Release mode
RUN dotnet publish MorkosiaPrepaLeague/MorkosiaPrepaLeague.csproj \
    -c Release \
    -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Create /data directory for LiteDB and grant ownership BEFORE switching user
RUN mkdir -p /data && chown -R appuser:appgroup /data

# Copy published output from build stage
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app
USER appuser

# Expose HTTP and HTTPS ports
EXPOSE 8080
EXPOSE 8081

# Use environment variable to configure ASP.NET Core URLs
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MorkosiaPrepaLeague.dll"]
