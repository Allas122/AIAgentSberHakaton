FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Domain/Domain.csproj Domain/
COPY ChatNode/ChatNode.csproj ChatNode/
RUN dotnet restore ChatNode/ChatNode.csproj

COPY Domain/ Domain/
COPY ChatNode/ ChatNode/
RUN dotnet publish ChatNode/ChatNode.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "ChatNode.dll"]
