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

# QuestPDF (invoice PDFs) needs fontconfig on Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ShormanServicesBackend.dll"]