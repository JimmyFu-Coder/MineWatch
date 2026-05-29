FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/MineWatch.Worker/MineWatch.Worker.csproj", "src/MineWatch.Worker/"]
COPY ["src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj", "src/MineWatch.Infrastructure/"]
COPY ["src/MineWatch.Contracts/MineWatch.Contracts.csproj", "src/MineWatch.Contracts/"]
RUN dotnet restore "src/MineWatch.Worker/MineWatch.Worker.csproj"

COPY . .
RUN dotnet publish "src/MineWatch.Worker/MineWatch.Worker.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MineWatch.Worker.dll"]
