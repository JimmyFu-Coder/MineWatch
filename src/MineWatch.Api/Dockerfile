FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/MineWatch.Api/MineWatch.Api.csproj", "src/MineWatch.Api/"]
COPY ["src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj", "src/MineWatch.Infrastructure/"]
RUN dotnet restore "src/MineWatch.Api/MineWatch.Api.csproj"

COPY . .
RUN dotnet publish "src/MineWatch.Api/MineWatch.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5211
ENV ASPNETCORE_URLS=http://+:5211

ENTRYPOINT ["dotnet", "MineWatch.Api.dll"]
