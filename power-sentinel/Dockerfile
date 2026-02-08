FROM node:24 AS node-build
WORKDIR /src
COPY package*.json ./
RUN npm ci
COPY . ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore "./power-sentinel.csproj" /p:SkipNodeBuild=true

# copy all and publish
COPY . ./
RUN dotnet publish "power-sentinel.csproj" -c Release -o /app/publish --no-restore /p:SkipNodeBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY --from=build /app/publish ./

# copy built static assets from node-build (Tailwind output)
COPY --from=node-build /src/wwwroot ./wwwroot

# persist sqlite DB outside the container
VOLUME ["/data"]
ENV DatabaseConnectionString="Data Source=/data/power-sentinel.db"
EXPOSE 80
ENTRYPOINT ["dotnet", "power-sentinel.dll"]
