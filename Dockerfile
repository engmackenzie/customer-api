FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY global.json CustomerManagement.slnx ./
COPY src/CustomerManagement.Api/CustomerManagement.Api.csproj src/CustomerManagement.Api/
RUN dotnet restore src/CustomerManagement.Api/CustomerManagement.Api.csproj
COPY src/CustomerManagement.Api/ src/CustomerManagement.Api/
RUN dotnet publish src/CustomerManagement.Api/CustomerManagement.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CustomerManagement.Api.dll"]
