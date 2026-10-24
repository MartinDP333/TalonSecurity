FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WafAdapter/WafAdapter.csproj", "WafAdapter/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "WafAdapter/WafAdapter.csproj"

COPY . .
WORKDIR /src/WafAdapter
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WafAdapter.dll"]
