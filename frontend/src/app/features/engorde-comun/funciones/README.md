# `funciones/` — lógica pura del módulo engorde-comun

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de
dependencias). Cada archivo agrupa una responsabilidad del modal de seguimiento engorde para que
sea **fácil de encontrar, testear y reutilizar**.

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, `HttpClient`
  ni estado del componente.
- Los componentes (`pages/`, `components/`) quedan como **orquestadores delgados**: arman los
  parámetros, llaman la función y manejan estado/HTTP/UI.
- Los tipos compartidos viven en [`../models/`](../models), no aquí (evita imports circulares).

## Índice

| Archivo | Qué hace |
|---|---|
| `fecha.funcion.ts` | `todayYMD`, `computeDefaultFecha`, `toYMD`, `ymdToIsoAtNoon` (helpers de fecha del formulario). |
| `inventario-calculos.funcion.ts` | `toNumOrNull`, `toKg`, `esUnidadDesconocidaParaGramos`, `cantidadOriginalAGramos`, `normalizarIdCatalogoSeleccion` (conversiones/aritmética de inventario). |
| `mapear-seguimiento-dto.funcion.ts` | Normalización (`normalizeJsonField`, `resolveItemCatalogId`, `getInventarioUbicacionFromLote`, `itemEcuadorToCatalogItem`) y armado del DTO de `onSave` (`construirItemsSeguimiento`, `construirItemsAdicionales`, `construirTipoAlimentoStr`, `aplicarCerosSinAvesDisponibles`, `mapearPanamaMixtoAHM`, `buildBaseSeguimientoDto`). |

## Nota multi-país

Esta capa es la **base común** para la organización multi-país (Colombia / Ecuador / Panamá). Los
modales por país deben **reutilizar** estas funciones y los `models/`, limitándose a la lógica de
presentación específica del país. Si una regla es común a todos los países, va aquí; si es propia de
un país, va en el componente/modal de ese país.

> **Aviso:** `mapearPanamaMixtoAHM` y `aplicarCerosSinAvesDisponibles` MUTAN el objeto `raw`
> recibido (mismo comportamiento que el `Object.assign(raw, ...)` original de `onSave`).
