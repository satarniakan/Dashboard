# ---- مرحله‌ی build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Dashboard.slnx ./
COPY Dashboard.Domain/Dashboard.Domain.csproj Dashboard.Domain/
COPY Dashboard.Application/Dashboard.Application.csproj Dashboard.Application/
COPY Dashboard.Infrastructure/Dashboard.Infrastructure.csproj Dashboard.Infrastructure/
COPY Dashboard.Web/Dashboard.Web.csproj Dashboard.Web/
RUN dotnet restore Dashboard.Web/Dashboard.Web.csproj
COPY Dashboard.Domain/ Dashboard.Domain/
COPY Dashboard.Application/ Dashboard.Application/
COPY Dashboard.Infrastructure/ Dashboard.Infrastructure/
COPY Dashboard.Web/ Dashboard.Web/
RUN dotnet publish Dashboard.Web/Dashboard.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- مرحله‌ی اجرا ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# کلیدهای DataProtection بین ری‌استارت کانتینر حفظ شوند (کوکی لاگین باطل نشود)
VOLUME ["/app/DataProtection-Keys"]

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# تصویر aspnet ابزار curl ندارد؛ با bash به پورت گوش می‌دهیم (HEALTHCHECK فقط CMD/NONE می‌پذیرد)
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD bash -c ':> /dev/tcp/127.0.0.1/8080' || exit 1

ENTRYPOINT ["dotnet", "Dashboard.Web.dll"]
