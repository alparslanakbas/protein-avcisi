FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/IndirimTakip.Api/IndirimTakip.Api.csproj IndirimTakip.Api/
COPY src/IndirimTakip.Core/IndirimTakip.Core.csproj IndirimTakip.Core/
COPY src/IndirimTakip.Infrastructure/IndirimTakip.Infrastructure.csproj IndirimTakip.Infrastructure/
RUN dotnet restore IndirimTakip.Api/IndirimTakip.Api.csproj

COPY src/ .
RUN dotnet publish IndirimTakip.Api/IndirimTakip.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render (ve benzeri platformlar) PORT ortam değişkenini kendisi atıyor —
# varsa onu kullan, yoksa 8080'e düş.
ENV PORT=8080
EXPOSE 8080

# appsettings.json'ı izleyen FileSystemWatcher, Render'ın konteyner dosya
# sisteminde native bir çökmeye (SIGABRT / status 134) yol açıyor — bu,
# kısıtlı/overlay dosya sistemli konteyner ortamlarında bilinen bir .NET
# sorunu. Config dosyalarını "değişince otomatik yenile" özelliğini
# kapatarak dosya izleyicisinin hiç kurulmasını engelliyoruz (biz zaten
# canlıda appsettings.json'ı çalışırken değiştirmiyoruz).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet IndirimTakip.Api.dll"]
