# Validación — cómputo que puede pasar a la BD (agilizar, reducir consumo, front sin cálculos)

> Objetivo del usuario: mover cálculo/transformación a funciones/vistas SQL para que la respuesta sea más ágil,
> el backend transforme menos y el **front NO calcule** (solo pinte). Clean code + buenas prácticas.
> Base: ya existe patrón sólido `fn_*`/`vw_*` en engorde Ecuador; replicarlo donde falta.

## Método por candidato (para NO cambiar resultados)
1. Crear función SQL (`fn_*`) que devuelva el resultado **ya armado** (filas/indicadores).
2. Endpoint delega con `SqlQueryRaw<Dto>` (patrón de `IndicadorEcuadorService` / `fn_seguimiento_diario_engorde`).
3. Front: reemplazar el cálculo por **consumir el DTO** (solo render). Quitar el servicio/compute del cliente.
4. **Test de equivalencia numérica**: el resultado SQL == el cómputo actual (mismos `Math.Round`, orden, residuos) — xUnit + captura de casos reales.
5. Validación E2E con la sesión del país + `dotnet build`/`yarn build`.
6. Migración EF idempotente que aplica la fn. No tocar vistas Power BI Ecuador.

## Grupo 1 — El FRONT calcula (prioridad ALTA: es lo que pidió el usuario)

| # | Módulo (país) | Dónde | Evidencia | Candidato |
|---|---|---|---|---|
| **C1** | Levante postura (Colombia) | `lote-levante/tabla-lista-indicadores` (1088 líneas, 186 ops) + `graficas-principal` (1350) | Reciben `seguimientos` crudos por `@Input` y arman `IndicadorSemanal[]` en cliente (consumo esperado, dif. vs guía, piso térmico, uniformidad…) | `fn_indicadores_levante_postura(loteId…)` → tabla semanal armada (espejo de `fn_seguimiento_diario_engorde`) |
| **C2** | Producción postura (Colombia) | `lote-produccion/tabla-lista-indicadores` (346) + `graficas-principal` (748) | idem, cálculo diario/semanal en front | `fn_indicadores_produccion_postura(...)` |
| **C3** | Engorde (Ec/Pa) | `engorde-comun/indicadores-diarios-engorde-compute.service` (385) | La **tabla** ya viene de `fn_seguimiento_diario_engorde`, pero los **indicadores vs guía** se recalculan en front | Extender la fn existente o `fn_indicadores_diarios_engorde` y borrar el compute del front |

> C1 es el de mayor impacto: es el componente que más calcula en cliente y es el perfil (postura Colombia) que el usuario usa hoy.

## Grupo 2 — El BACK agrega en C# (prioridad MEDIA: reduce consumo backend)

Ranking por `GroupBy`/`ToListAsync` en memoria (medido en ciclos previos):

| # | Servicio | Tamaño | Señal |
|---|---|---|---|
| **B1** | `ReporteTecnicoService` | 3.110 líneas | 5 GroupBy / 21 ToListAsync |
| **B2** | `ReporteTecnicoProduccionService` | 1.953 | 6 / 22 |
| **B3** | `ReporteContableService` | 1.458 | 7 / 12 |

Candidatos a `fn_*` que devuelvan el reporte armado (patrón `fn_informe_semanal_pollo_engorde`). Reducen viajes a BD (N queries → 1) y CPU de transformación en el pod ECS.

## Ya bien resuelto (referencia, NO tocar)
- Engorde Ecuador: `fn_indicadores_pollo_engorde`, `fn_seguimiento_diario_engorde`, `fn_informe_semanal_pollo_engorde`, `fn_auditoria_liquidacion_engorde` + vistas Power BI. Es el modelo a replicar.
- `MovimientoPolloEngordeService.ResumenDisponibilidad`: evaluado y descartado (write-path crítico ya batcheado; migrar = riesgo de deriva C#↔SQL).

## Recomendación de arranque
Empezar por **C1 (indicadores levante postura → SQL)**: máximo impacto en "front sin cálculos", perfil activo, y hay un patrón espejo probado (engorde) para copiar con bajo riesgo. Un candidato por ciclo, con test de equivalencia antes de borrar el cálculo del front.

## Riesgos / salvaguardas
- Aritmética idéntica obligatoria (redondeos/orden) → test de equivalencia antes de retirar el cómputo viejo.
- No romper contratos: el DTO nuevo debe mapear a lo que el template ya espera.
- Postura Colombia tiene bugs históricos de comparación vs guía (ver memoria postura-colombia) → congelar el comportamiento actual en tests antes de migrar.
