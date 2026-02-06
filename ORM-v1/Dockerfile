FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app
COPY . .

RUN dotnet restore
RUN dotnet build -c Release

CMD ["dotnet", "test", "--no-build", "--verbosity", "normal"]
