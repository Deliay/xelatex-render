FROM archlinux AS base
USER root
RUN pacman -Syu --noconfirm --needed
RUN pacman -S texlive texlive-lang
RUN tlmgr install exam-zh
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

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
RUN dotnet publish "./XeLatexRender.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["XeLatexRender.Api"]
