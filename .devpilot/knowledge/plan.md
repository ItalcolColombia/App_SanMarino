# Plan propuesto: realia esta tarea tengo archivos tocados realiza un commit de esos arc

**Objetivo:** realia esta tarea tengo archivos tocados realiza un commit de esos archivo y sigue con las misiones > Riesgos y misiones sugeridas
⚠ 3 archivo(s) superan 400 líneas (posible deuda técnica).

⚠ Módulo con mayor deuda técnica: "backend" — 3 archivo(s) de más de 400 líneas; poco test (0 de 3 archivos).

## Estrategia
Monorepo Clean Architecture: backend .NET 10 (API/Application/Infrastructure/Domain, EF Core 10 + Npgsql) y frontend Angular 22 standalone. El working tree tiene ~80 archivos tocados que cruzan tres contextos: consolidación de entidades de seguimiento/producción (entidades y configuraciones ProduccionDiaria/ProduccionSeguimiento eliminadas, snapshot EF actualizado), eliminación del módulo viejo frontend/features/inventario (reemplazado por gestion-inventario), y ajustes en servicios de movimientos/liquidación. Verifiqué líneas reales: los archivos tocados que superan el umbral son MovimientoAvesService.cs (2507), SeguimientoAvesEngordeService.cs (1884), IndicadorEcuadorService.cs (1185), y además SeguimientoAvesEngordeEcuadorService.cs (1087) y MovimientoAvesController.cs (1019) también violan el gate de 800 líneas. Orden de ataque: (1) validar build+tests y auditar el diff para excluir archivos con credenciales (appsettings.Development.json y launchSettings.json están tocados y esa es la fuente de credenciales locales — constitución #5) antes de commitear; (2) commit del trabajo en curso agrupado por alcance, autor único moisesmurillo sin atribución a Claude (preferencia registrada del usuario); (3) refactor de deuda con el patrón canónico del repo (partial class en Services/<Modulo>/Funciones/ + cálculo puro en Application/Calculos/ + tests xUnit de equivalencia), un servicio por tarea para mantener diffs revisables, empezando por el peor (MovimientoAvesService). Refactor ≠ cambio de comportamiento: aritmética y contratos idénticos, validado con dotnet build + dotnet test tras cada partición.

## Alineación con la visión
No se proporcionó una visión de producto explícita; la misión es higiene del repositorio (consolidar trabajo en curso bajo Git y reducir deuda técnica en backend), lo cual es ortogonal a features pero mejora directamente la mantenibilidad y cumple el gate arquitectónico de 800 líneas que hoy está violado por 5 archivos tocados.

## Commit seguro del trabajo en curso
Consolidar los ~80 archivos modificados/eliminados del working tree en commits limpios y auditados, sin secretos y sin romper el build, cumpliendo el workflow obligatorio de CLAUDE.md (plan + tracker).

- Plan y tracker de la misión — `documentation` · documentation
- Auditoría del diff y filtro de secretos — `review` · security
- Validación de build pre-commit — `tests` · qa
- Crear commits agrupados por alcance — `crud` · devops

## Reducción de deuda técnica backend (archivos >800 líneas)
Aplicar el patrón canónico del repo (movimientos-pollo-engorde): partir servicios largos en partial classes bajo Services/<Modulo>/Funciones/ con namespace plano, extraer matemática pura a Application/Calculos/ y cubrirla con tests xUnit de equivalencia. Refactor sin cambio de comportamiento: interfaz, DI, contratos y aritmética (Math.Round, orden, residuos) idénticos.

- Refactor MovimientoAvesService (2507 líneas) a partials + cálculo puro — `complex-refactor` · backend
- Tests de equivalencia MovimientoAvesCalculos — `tests` · qa
- Refactor SeguimientoAvesEngordeService (1884 líneas) — `complex-refactor` · backend
- Tests de equivalencia SeguimientoAvesEngordeCalculos — `tests` · qa
- Refactor IndicadorEcuadorService (1185) y SeguimientoAvesEngordeEcuadorService (1087) — `complex-refactor` · backend
- Refactor MovimientoAvesController (1019 líneas) — `complex-refactor` · backend
- Tests de indicadores Ecuador — `tests` · qa

## Validación final y cierre
Compuerta de calidad de toda la misión: build verde, tests verdes, gate de 800 líneas cumplido, tracker actualizado y conocimiento registrado.

- Build + suite completa + verificación del gate de líneas — `tests` · qa
- Cierre: tracker y base de conocimiento — `summary` · documentation
