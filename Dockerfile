FROM mcr.microsoft.com/dotnet/sdk:9.0-preview

WORKDIR /app
COPY . .

RUN dotnet restore

RUN dotnet build ORM.Tests/ORM.Tests.csproj -c Debug

CMD ["dotnet", "test", "ORM.Tests/ORM.Tests.csproj", "-c", "Debug", "--no-build", "--verbosity", "normal"]

