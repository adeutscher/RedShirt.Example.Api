FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS base
# hadolint ignore=DL3066
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /build
COPY src src
COPY test test
COPY swagger swagger
COPY *.slnx .
COPY global.json .

RUN dotnet restore \
 && dotnet build -c $BUILD_CONFIGURATION
ARG TESTS_ENABLE=1
# hadolint ignore=SC1072,SC2288
RUN \[ ${TESTS_ENABLE} -ne 1 \] \
  || \
      ([ -d "test" \] \
      && dotnet test )

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# The build stage has already built the API and regenerated swagger.json, so NSwag is skipped here.
RUN rm -rf swagger test/ ./*slnx global.json \
 && dotnet publish "src/RedShirt.Example.Api/RedShirt.Example.Api.csproj" --self-contained -c $BUILD_CONFIGURATION -o /app/publish -p:SkipNSwag=true \
 && find /app/publish -type d -exec chmod 500 {} + \
 && find /app/publish -type f -exec chmod 400 {} + \
 && chmod 500 /app/publish/RedShirt.Example.Api

FROM base AS final
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["./RedShirt.Example.Api"]
