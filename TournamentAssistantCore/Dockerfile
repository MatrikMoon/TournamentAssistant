#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0-bullseye-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS publish
WORKDIR /src
COPY ["TournamentAssistantCore/TournamentAssistantCore.csproj", "TournamentAssistantCore/"]
RUN dotnet restore "TournamentAssistantCore/TournamentAssistantCore.csproj"
COPY . .
WORKDIR "/src/TournamentAssistantCore"
RUN dotnet publish "TournamentAssistantCore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TournamentAssistantCore.dll"]