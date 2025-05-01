FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app
COPY . ./

ENV DotNetTargettingPacksTargetsPath10=true

# Restore dependencies
RUN dotnet restore BusInfo.csproj
# Build the project
RUN dotnet build BusInfo.csproj -c Release -o out --framework net10.0
# Publish the project
RUN dotnet publish BusInfo.csproj -c Release -o out --framework net10.0

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app

# Install dependencies for certificate handling
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       ca-certificates \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out ./

COPY --from=build /app/appsettings*.json ./
RUN if [ ! -f "appsettings.json" ] && [ -f "appsettings.example.json" ]; then \
    cp appsettings.example.json appsettings.json; \
    elif [ ! -f "appsettings.json" ]; then \
    echo "ERROR: Neither appsettings.json nor appsettings.example.json found" && exit 1; \
    fi

EXPOSE 3001

ENV ASPNETCORE_ENVIRONMENT=Production

# Command to run the application
ENTRYPOINT ["dotnet", "BusInfo.dll"]
CMD ["--environment=Production"]