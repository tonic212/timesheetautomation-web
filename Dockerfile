FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TimesheetAutomation.Web.csproj", "./"]
RUN dotnet restore "./TimesheetAutomation.Web.csproj"

COPY . .
RUN dotnet publish "./TimesheetAutomation.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:7152
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN mkdir -p /app/App_Data /app/Exports /app/Templates

EXPOSE 7152

ENTRYPOINT ["dotnet", "TimesheetAutomation.Web.dll"]