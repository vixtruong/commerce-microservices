FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
ARG PROJECT_PATH
COPY . .
RUN dotnet restore "$PROJECT_PATH"

FROM restore AS publish
ARG PROJECT_PATH
RUN dotnet publish "$PROJECT_PATH" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ARG ASSEMBLY_NAME
ENV APP_ASSEMBLY=$ASSEMBLY_NAME \
    ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8080 8081
ENTRYPOINT ["sh", "-c", "exec dotnet /app/$APP_ASSEMBLY.dll"]
