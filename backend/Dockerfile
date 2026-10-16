FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CommentsApp.Api/CommentsApp.Api.csproj src/CommentsApp.Api/
COPY src/CommentsApp.Domain/CommentsApp.Domain.csproj src/CommentsApp.Domain/
COPY src/CommentsApp.Infrastructure/CommentsApp.Infrastructure.csproj src/CommentsApp.Infrastructure/
RUN dotnet restore src/CommentsApp.Api/CommentsApp.Api.csproj

COPY src/ src/
RUN dotnet publish src/CommentsApp.Api/CommentsApp.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /app/uploads && chown app:app /app/uploads
USER app

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "CommentsApp.Api.dll"]