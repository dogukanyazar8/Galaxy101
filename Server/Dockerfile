# Build context: repository root (the server compiles the Unity rules engine sources).
#   docker build -f Server/Dockerfile -t galaxy101-server .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Server/Directory.Build.props Server/
COPY Server/src/Galaxy101.Rules/Galaxy101.Rules.csproj Server/src/Galaxy101.Rules/
COPY Server/src/Galaxy101.Table/Galaxy101.Table.csproj Server/src/Galaxy101.Table/
COPY Server/src/Galaxy101.Server/Galaxy101.Server.csproj Server/src/Galaxy101.Server/
COPY Shared/Galaxy101.Contracts/Galaxy101.Contracts.csproj Shared/Galaxy101.Contracts/
RUN dotnet restore Server/src/Galaxy101.Server/Galaxy101.Server.csproj
COPY Packages/com.okey101.core/Runtime Packages/com.okey101.core/Runtime
COPY Packages/com.galaxy101.table/Runtime Packages/com.galaxy101.table/Runtime
COPY Assets/Okey101/Bots/BotPlanner.cs Assets/Okey101/Bots/
COPY Shared/Galaxy101.Contracts Shared/Galaxy101.Contracts
COPY Packages/com.galaxy101.contracts/Runtime Packages/com.galaxy101.contracts/Runtime
COPY Server/src Server/src
RUN dotnet publish Server/src/Galaxy101.Server/Galaxy101.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Galaxy101.Server.dll"]
