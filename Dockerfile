FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy project and restore
COPY . .
RUN dotnet restore BusInfo.csproj
RUN dotnet publish BusInfo.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /out ./

ENV ASPNETCORE_URLS=http://*:80
EXPOSE 80

ENTRYPOINT ["dotnet", "BusInfo.dll"]