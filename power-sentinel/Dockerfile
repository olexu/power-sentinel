FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore "./power-sentinel.csproj"

# copy all and publish
COPY . ./
RUN dotnet publish "power-sentinel.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY --from=build /app/publish ./

# persist sqlite DB outside the container
VOLUME ["/data"]
ENV ConnectionStrings__Default="Data Source=/data/power-sentinel.db"
EXPOSE 80
ENTRYPOINT ["dotnet", "power-sentinel.dll"]
