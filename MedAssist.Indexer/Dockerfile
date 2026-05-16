FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MedAssist.Shared/MedAssist.Shared.csproj MedAssist.Shared/
COPY MedAssist.AI/MedAssist.AI.csproj MedAssist.AI/
COPY MedAssist.Indexer/MedAssist.Indexer.csproj MedAssist.Indexer/
RUN dotnet restore MedAssist.Indexer/MedAssist.Indexer.csproj

COPY MedAssist.Shared/ MedAssist.Shared/
COPY MedAssist.AI/ MedAssist.AI/
COPY MedAssist.Indexer/ MedAssist.Indexer/
RUN dotnet publish MedAssist.Indexer/MedAssist.Indexer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY config/ /config/
ENV MEDASSIST_CONFIG_PATH=/config
ENTRYPOINT ["dotnet", "MedAssist.Indexer.dll"]
