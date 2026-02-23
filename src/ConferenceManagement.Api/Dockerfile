FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/ConferenceManagement.Api/ConferenceManagement.Api.csproj", "ConferenceManagement.Api/"]
RUN dotnet restore "ConferenceManagement.Api/ConferenceManagement.Api.csproj"

COPY src/ConferenceManagement.Api/ ConferenceManagement.Api/
WORKDIR /src/ConferenceManagement.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ConferenceManagement.Api.dll"]
