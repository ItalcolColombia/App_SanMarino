# Plan — Upgrade backend .NET 9 → 10 (LTS)

> Estado: proyecto en `net9.0` (STS, maintenance, EOL 2026-11-10). Objetivo: **.NET 10.0.9 LTS** (soporte a 2028-11-14).
> SDK 10 NO instalado (hay 8.0.408 + 9.0.301) → instalar **user-scope sin admin** (dotnet-install, como el Node portable).

## Enfoque
1. **Instalar SDK .NET 10** en carpeta de usuario: `dotnet-install.ps1 -Channel 10.0 -InstallDir C:\Users\SAN MARINO\dotnet-portable` (sin admin). Usar ese `dotnet` para build/test.
2. **Target framework:** `net9.0` → `net10.0` en todos los `.csproj` (API, Application, Infrastructure, Domain, Tests).
3. **Paquetes NuGet `Microsoft.*` 9.x → 10.x:** EF Core (`Microsoft.EntityFrameworkCore*`, `Npgsql.EntityFrameworkCore.PostgreSQL`), ASP.NET, `Microsoft.Extensions.*`, etc. Vía `dotnet outdated`/edición de `.csproj` + `dotnet restore`.
4. **Build + test + arreglar breaking changes** de .NET 10 / EF 10.
5. Commit.

## Riesgos / cuidados
- EF Core 10 puede traer breaking changes (queries, migraciones) → **no romper el historial de migraciones** ni la aritmética validada.
- Npgsql: subir a la versión compatible con EF 10.
- Refactor ≠ cambio de comportamiento (contratos/lógica/aritmética intactos).
- El deploy (ECS) usa su propio SDK — verificar que el runtime .NET 10 esté disponible en la imagen/base al desplegar (Dockerfile backend).

## Validación
- `dotnet build` 0/0 (con SDK 10) + `dotnet test` 122 verdes.
- Migraciones: `dotnet ef migrations list` sin romper; app levanta local.

## Pasos
- [ ] Instalar SDK .NET 10 user-scope
- [ ] `net9.0`→`net10.0` en .csproj
- [ ] Subir paquetes NuGet 9→10 (EF/ASP.NET/Extensions/Npgsql)
- [ ] build + test + fix
- [ ] commit
