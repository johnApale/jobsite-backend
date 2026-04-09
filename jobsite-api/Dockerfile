# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore Jobsite.Api.slnx

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Jobsite.Api/Jobsite.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

COPY --from=build /app/publish .

USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Jobsite.Api.dll"]
