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

RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

RUN dotnet ef database update

ENTRYPOINT ["dotnet", "BusInfo.dll"]