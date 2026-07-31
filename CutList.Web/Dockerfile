# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY CutList.sln .
COPY CutList.Web/CutList.Web.csproj CutList.Web/
COPY CutList.Core/CutList.Core.csproj CutList.Core/
RUN dotnet restore CutList.Web/CutList.Web.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/CutList.Web
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5270

ENV ASPNETCORE_URLS=http://+:5270
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CutList.Web.dll"]
