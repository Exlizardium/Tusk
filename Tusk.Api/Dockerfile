FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Tusk.Api.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

FROM build AS migrations-build
RUN dotnet tool install --global dotnet-ef --version 10.0.8
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --configuration Release --output /migrations/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /migrations
COPY --from=migrations-build /migrations/efbundle ./
USER app
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tusk.Api.dll"]
