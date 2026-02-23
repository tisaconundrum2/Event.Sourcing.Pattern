FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/ConferenceManagement.MessageQueue/ConferenceManagement.MessageQueue.csproj", "ConferenceManagement.MessageQueue/"]
RUN dotnet restore "ConferenceManagement.MessageQueue/ConferenceManagement.MessageQueue.csproj"

COPY src/ConferenceManagement.MessageQueue/ ConferenceManagement.MessageQueue/
WORKDIR /src/ConferenceManagement.MessageQueue
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ConferenceManagement.MessageQueue.dll"]
