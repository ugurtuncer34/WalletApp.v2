# 1. Runtime env
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# 2. Compile env
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["WalletApp.csproj", "./"]
RUN dotnet restore "WalletApp.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "WalletApp.csproj" -c Release -o /app/build

# 3. Publish env
FROM build AS publish
RUN dotnet publish "WalletApp.csproj" -c Release -o /app/publish

# 4. Copy app to basic ver and run
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WalletApp.dll"]