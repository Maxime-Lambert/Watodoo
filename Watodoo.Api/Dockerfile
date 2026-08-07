# Build context = racine du repo (pas Watodoo.Api/) pour que MSBuild retrouve
# Directory.Build.props / Directory.Packages.props (Central Package Management)
# en remontant l'arbre depuis le csproj, comme en local.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY Watodoo.Api/Watodoo.Api.csproj Watodoo.Api/
RUN dotnet restore Watodoo.Api/Watodoo.Api.csproj

COPY Watodoo.Api/ Watodoo.Api/
RUN dotnet publish Watodoo.Api/Watodoo.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Watodoo.Api.dll"]
