﻿FROM registry.cn-hangzhou.aliyuncs.com/zero-tools/xelatex-render-base:latest AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
USER root
RUN useradd tex
USER tex

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["XeLatexRender.Api/XeLatexRender.Api.csproj", "XeLatexRender.Api/"]
RUN dotnet restore "XeLatexRender.Api/XeLatexRender.Api.csproj"
COPY . .
WORKDIR "/src/XeLatexRender.Api"
RUN dotnet build "./XeLatexRender.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN apt update
RUN apt install clang -y
RUN dotnet publish "./XeLatexRender.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/app/XeLatexRender.Api"]
