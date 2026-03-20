FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 7152
ENV ASPNETCORE_URLS=http://+:7152

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TimesheetAutomation.Web.csproj", "./"]
RUN dotnet restore "TimesheetAutomation.Web.csproj"
COPY . .
RUN dotnet publish "TimesheetAutomation.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TimesheetAutomation.Web.dll"]