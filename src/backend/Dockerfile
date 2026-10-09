FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG LIFEOS_BUILD_ID=local
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
ARG LIFEOS_BUILD_ID=local
ARG LIFEOS_VERSION_MAJOR=1
ARG LIFEOS_BUILD_NUMBER
ARG LIFEOS_RUN_ID
ARG LIFEOS_RUN_ATTEMPT
ARG LIFEOS_BUILD_REPOSITORY
ARG LIFEOS_BUILD_SERVER_URL
ARG LIFEOS_BUILD_WORKFLOW
WORKDIR /app

COPY --from=build /app/publish .
USER app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV LifeOS__BuildId=${LIFEOS_BUILD_ID}
ENV LifeOS__VersionMajor=${LIFEOS_VERSION_MAJOR}
ENV LifeOS__BuildNumber=${LIFEOS_BUILD_NUMBER}
ENV LifeOS__RunId=${LIFEOS_RUN_ID}
ENV LifeOS__RunAttempt=${LIFEOS_RUN_ATTEMPT}
ENV LifeOS__Repository=${LIFEOS_BUILD_REPOSITORY}
ENV LifeOS__ServerUrl=${LIFEOS_BUILD_SERVER_URL}
ENV LifeOS__Workflow=${LIFEOS_BUILD_WORKFLOW}
EXPOSE 8080

ENTRYPOINT ["dotnet", "LifeOS.Api.dll"]
