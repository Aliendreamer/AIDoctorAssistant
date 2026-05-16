FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MedAssist.Shared/MedAssist.Shared.csproj MedAssist.Shared/
COPY MedAssist.AI/MedAssist.AI.csproj MedAssist.AI/
COPY MedAssist.Web/MedAssist.Web.csproj MedAssist.Web/
RUN dotnet restore MedAssist.Web/MedAssist.Web.csproj

COPY MedAssist.Shared/ MedAssist.Shared/
COPY MedAssist.AI/ MedAssist.AI/
COPY MedAssist.Web/ MedAssist.Web/
RUN dotnet publish MedAssist.Web/MedAssist.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY config/ /config/
ENV MEDASSIST_CONFIG_PATH=/config
EXPOSE 8080
ENTRYPOINT ["dotnet", "MedAssist.Web.dll"]
