# Plan propuesto: busca archivos que este muy largos y dividilos octimizando foram de le

**Objetivo:** busca archivos que este muy largos y dividilos octimizando foram de lectura tanto en la carpeta front y carpeta back , valida todo el proyecto y comenzemos a organizar todo el codigo completo

## Estrategia
Backend sigue Clean Architecture (.NET 10: API/Application/Infrastructure/Domain) y ya tiene un patrón canónico de refactor definido en CLAUDE.md (partial classes en `Funciones/` + cálculo puro en `Application/Calculos/`, referencia: módulo movimientos-pollo-engorde). Verifiqué en el repo actual (no en memoria vieja) que los principales infractores siguen intactos: `MovimientoAvesService.cs` 2507 líneas, `SeguimientoAvesEngordeService.cs` 1884, `IndicadorEcuadorService.cs` 1185, `SeguimientoAvesEngordeEcuadorService.cs` 1087, `MovimientoAvesController.cs` 1019, `SeguimientoLoteLevanteService.cs` 837 (supera el límite de 800 recién por poco). El frontend (Angular 22 standalone) tiene su propio patrón (`funciones/`+`models/`) pero no fue auditado todavía, así que el ataque empieza con una auditoría completa (back+front) para no repetir trabajo ya hecho ni perder archivos nuevos. Orden: auditar → refactorizar backend de mayor a menor deuda (servicio+tests de equivalencia antes de tocar el controller que depende de él) → refactorizar frontend con el mismo criterio → build+test completo de ambos stacks → cerrar tracker y knowledge base. Cada paso deja el sistema compilando y con comportamiento idéntico (refactor ≠ cambio de lógica), evitando el rewrite big-bang.

## Alineación con la visión
El repo no tiene una visión de producto documentada (el template está vacío), así que no hay north star estratégico contra el cual medir. Sin embargo esta misión sirve directamente al estándar de ingeniería vinculante del propio CLAUDE.md (clean code, orquestadores delgados, gate de líneas) y reduce una deuda técnica que ya bloqueó una misión anterior — es una mejora de mantenibilidad pura, sin tocar reglas de negocio ni UI.

## Veredicto del CTO (salud arquitectónica)
✓ **approve** (tendencia: stable)

- La arquitectura está sana.

### Evidencia
- Sin línea base previa: se registra el estado actual como referencia (tendencia estable).
- Ciclos de dependencia: 0 actual(es).
- Concentración de hotspots (top-10 PageRank): 12.5%.
- Violaciones del Architecture Guardian: 0.

## CFO (estimación de costo)
Estimación: $6.85 (modelo base: claude-sonnet-5)

## Auditoría de archivos largos (front + back)
Relevar todo el repo para tener el inventario completo y priorizado antes de tocar código.

- Auditar backend y frontend en busca de archivos largos — `architecture` · architect

## Backend — reducción de deuda técnica (partials + cálculo puro)
Dividir los servicios/controllers más largos del backend siguiendo el patrón partial class + Application/Calculos, sin cambiar comportamiento.

- Refactor MovimientoAvesService (2507 líneas) — `complex-refactor` · backend
- Tests de equivalencia MovimientoAvesCalculos — `tests` · qa
- Refactor MovimientoAvesController (1019 líneas) — `complex-refactor` · backend
- Refactor SeguimientoAvesEngordeService (1884 líneas) — `complex-refactor` · backend
- Tests de equivalencia SeguimientoAvesEngordeCalculos — `tests` · qa
- Refactor IndicadorEcuadorService (1185) y SeguimientoAvesEngordeEcuadorService (1087) — `complex-refactor` · backend
- Tests de indicadores Ecuador — `tests` · qa
- Refactor SeguimientoLoteLevanteService (837 líneas) — `complex-refactor` · backend

## Frontend — componentes/servicios largos
Aplicar el patrón funciones/+models/ a los archivos Angular más largos detectados en la auditoría.

- Priorizar archivos frontend a refactorizar — `architecture` · frontend
- Refactor de archivos frontend priorizados — `complex-refactor` · frontend

## Validación global
Confirmar que ambos stacks compilan y los tests pasan tras todos los refactors.

- Build + suite completa backend + gate de líneas — `tests` · qa
- Build frontend de validación — `tests` · qa

## Cierre
Dejar registro reproducible del trabajo realizado.

- Actualizar tracker y base de conocimiento — `documentation` · documentation
