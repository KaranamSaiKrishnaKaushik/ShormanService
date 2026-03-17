FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ShormanService.slnx .
COPY ShormanService.Domain/ShormanService.Domain.csproj ShormanService.Domain/
COPY ShormanService.Application/ShormanService.Application.csproj ShormanService.Application/
COPY ShormanService.Infrastructure/ShormanService.Infrastructure.csproj ShormanService.Infrastructure/
COPY ShormanService.API/ShormanService.API.csproj ShormanService.API/

RUN dotnet restore

COPY . .
RUN dotnet publish ShormanService.API/ShormanService.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80

ENTRYPOINT ["dotnet", "ShormanService.API.dll"]
