# Use .NET 9 SDK image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore Library/Library/Library.csproj
RUN dotnet publish Library/Library/Library.csproj -c Release -o /app/publish

# Use .NET 9 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "Library.dll"]
