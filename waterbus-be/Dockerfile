# syntax=docker/dockerfile:1
# ==============================================================================
# Smart Waterbus API (.NET 8) — multi-stage build, chạy bằng user không phải root
# Build context: thư mục waterbus-be/
# ==============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy riêng các file .csproj trước để cache lớp restore khi chỉ đổi code
COPY src/WaterbusSystem.Domain/WaterbusSystem.Domain.csproj               src/WaterbusSystem.Domain/
COPY src/WaterbusSystem.Application/WaterbusSystem.Application.csproj     src/WaterbusSystem.Application/
COPY src/WaterbusSystem.Infrastructure/WaterbusSystem.Infrastructure.csproj src/WaterbusSystem.Infrastructure/
COPY src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj               src/WaterbusSystem.WebApi/
RUN dotnet restore src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

# Thư mục lưu khoá DataProtection (mount volume để không mất khi tạo lại container)
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID /home/app/.aspnet

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "WaterbusSystem.WebApi.dll"]
