FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ShormanServicesBackend.sln ./
COPY ShormanServicesBackend/ShormanServicesBackend.csproj ShormanServicesBackend/
RUN dotnet restore ShormanServicesBackend.sln

COPY ShormanServicesBackend/. ./ShormanServicesBackend/

WORKDIR /src/ShormanServicesBackend
RUN dotnet publish ShormanServicesBackend.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ShormanServicesBackend.dll"]