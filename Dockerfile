# Use the .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy everything and restore dependencies
COPY . .
RUN dotnet restore ./Library/Library.csproj

# Build and publish the app
RUN dotnet publish ./Library/Library.csproj -c Release -o /out

# Use the .NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /out .

# Expose the port your app listens on
EXPOSE 80
ENTRYPOINT ["dotnet", "Library.dll"]
