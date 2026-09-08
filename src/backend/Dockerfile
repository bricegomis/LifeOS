FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LifeOS.slnx ./
COPY src/LifeOS.Domain/LifeOS.Domain.csproj src/LifeOS.Domain/
COPY src/LifeOS.Application/LifeOS.Application.csproj src/LifeOS.Application/
COPY src/LifeOS.Infrastructure/LifeOS.Infrastructure.csproj src/LifeOS.Infrastructure/
COPY src/LifeOS.Api/LifeOS.Api.csproj src/LifeOS.Api/
RUN dotnet restore src/LifeOS.Api/LifeOS.Api.csproj

COPY src/ src/
RUN dotnet publish src/LifeOS.Api/LifeOS.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
USER app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LifeOS.Api.dll"]
