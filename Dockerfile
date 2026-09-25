FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FiapDonateUsers.slnx", "./"]
COPY ["FiapDonateUsers.API/FiapDonateUsers.API.csproj", "FiapDonateUsers.API/"]
COPY ["FiapDonateUsers.Application/FiapDonateUsers.Application.csproj", "FiapDonateUsers.Application/"]
COPY ["FiapDonateUsers.Domain/FiapDonateUsers.Domain.csproj", "FiapDonateUsers.Domain/"]
COPY ["FiapDonateUsers.Infrastructure/FiapDonateUsers.Infrastructure.csproj", "FiapDonateUsers.Infrastructure/"]
RUN dotnet restore "FiapDonateUsers.API/FiapDonateUsers.API.csproj"

COPY . .
RUN dotnet publish "FiapDonateUsers.API/FiapDonateUsers.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FiapDonateUsers.API.dll"]
