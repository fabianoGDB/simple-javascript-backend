# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore SchoolETL/SchoolETL.csproj
RUN dotnet publish SchoolETL/SchoolETL.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 5149
ENV ASPNETCORE_URLS=http://+:5149
ENTRYPOINT ["dotnet", "SchoolETL.dll"]
