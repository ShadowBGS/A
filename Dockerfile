# Use the official .NET SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Library/Library/Library/*.csproj ./Library/
RUN dotnet restore ./Library/Library.csproj

# Copy everything else and build
COPY . .
WORKDIR /app/Library/Library/Library
RUN dotnet publish -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Library.dll"]
