# Backend API — .NET 10
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# SkiaSharp/PDFium (conversión PDF→imagen) necesita esta librería nativa en Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/ClassificadorExtractorDocumentos.Api -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClassificadorExtractorDocumentos.Api.dll"]
