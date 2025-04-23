# Use the official .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy the whole repo
COPY . .

# Restore using the correct path
RUN dotnet restore Library/Library/Library.csproj

# Build and publish
RUN dotnet publish Library/Library/Library.csproj -c Release -o /app/publish

# Use the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose the default port
EXPOSE 80

# Launch the app
ENTRYPOINT ["dotnet", "Library.dll"]
