# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Jobsite.Api/Jobsite.Api.csproj

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Jobsite.Api/Jobsite.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && \
    rm -rf /var/lib/apt/lists/* && \
    groupadd --system --gid 1001 appgroup && \
    useradd --system --uid 1001 --gid appgroup --no-create-home appuser

COPY --from=build /app/publish .

USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Jobsite.Api.dll"]
