FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY global.json BuildWithAi.slnx ./
COPY src/BuildWithAi.Api/BuildWithAi.Api.csproj src/BuildWithAi.Api/
RUN dotnet restore src/BuildWithAi.Api/BuildWithAi.Api.csproj
COPY src/BuildWithAi.Api/ src/BuildWithAi.Api/
RUN dotnet publish src/BuildWithAi.Api/BuildWithAi.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BuildWithAi.Api.dll"]
