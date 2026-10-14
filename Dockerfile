FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Portfolio.Api.csproj backend/
RUN dotnet restore backend/Portfolio.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/Portfolio.Api.csproj -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
COPY index.html config.js ./site/
COPY menu/ ./site/menu/
COPY sobre/ ./site/sobre/
COPY foco/ ./site/foco/
COPY alem-do-codigo/ ./site/alem-do-codigo/
COPY contato/ ./site/contato/
COPY assets/ ./site/assets/
COPY shared/ ./site/shared/
COPY exercicios/ ./site/exercicios/
COPY apoio/ ./site/apoio/
COPY login/ ./site/login/
COPY alunos/ ./site/alunos/
COPY entregas/ ./site/entregas/
COPY termos/ ./site/termos/
COPY amigos/ ./site/amigos/
COPY mensagens/ ./site/mensagens/
COPY perfil/ ./site/perfil/
ENV SiteRoot=/app/site ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet","Portfolio.Api.dll"]
