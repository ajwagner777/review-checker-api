# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AW.ReviewChecker.Api/AW.ReviewChecker.Api.csproj", "src/AW.ReviewChecker.Api/"]
RUN dotnet restore "src/AW.ReviewChecker.Api/AW.ReviewChecker.Api.csproj"

COPY . .
WORKDIR "/src/src/AW.ReviewChecker.Api"
RUN dotnet publish "AW.ReviewChecker.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AW.ReviewChecker.Api.dll"]
