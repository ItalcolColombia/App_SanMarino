# 25 — Refactor Clean Code (Backend): `MovimientoPolloEngordeService`

> **Objetivo:** distribuir las funciones largas del servicio god (1903 líneas) en archivos
> **`partial class`** dentro de una subcarpeta **`Funciones/`** del módulo, agrupados por
> responsabilidad. Mismo patrón conceptual que el refactor del frontend: *clean code sin cambiar
> comportamiento*. La clase sigue siendo UNA sola (partial), por lo que DI, la interfaz y la lógica
> quedan intactas.

---

## 1. Estado actual

`Infrastructure/Services/MovimientoPolloEngordeService.cs` = **1903 líneas**, 1 clase, 33 miembros.
Métodos más largos:

| Método | Líneas aprox. |
|---|---|
| `AuditarVentasEngordeAsync` | ~286 |
| `GetAvesDisponiblesLotesAsync` | ~202 |
| `CorregirVentasCompletadasAsync` | ~161 |
| `CreateVentaGranjaDespachoAsync` | ~155 |
| `OrganizarPesoAsync` | ~114 |
| loaders de resumen (Ave Engorde / Reproductora) | ~101 / ~97 |

Sin `#region`, sin código muerto (todos los helpers privados se usan), sin funciones locales.

---

## 2. Por qué `partial class` (y no extraer funciones puras)

- Los métodos largos son **orquestación de BD** (EF Core: `_ctx`, transacciones, mutación de
  entidades). No son lógica pura → no se pueden mover a estáticos sin reescribir.
- `partial class` permite **repartir los métodos en varios archivos/carpetas** manteniendo acceso a
  `_ctx`, `_currentUser` y a todos los helpers privados, **sin tocar DI ni la interfaz**. Es el
  mecanismo idiomático de C# para exactamente lo que se pide ("distribuir funciones largas en
  subcarpetas").
- **Riesgo casi nulo**: es un movimiento mecánico de bloques de métodos; el comportamiento no cambia.

> El proyecto ya tiene `Application/Calculos/` para cálculo puro (`IndicadorEcuadorCalculos`). La
> extracción de math puro (p. ej. prorrateo de peso) a esa capa queda como **follow-up** opcional
> (ver §6); este pass se centra en la distribución segura por responsabilidad.

---

## 3. Estructura objetivo

```
Infrastructure/Services/
├── MovimientoPolloEngorde/                                  ← NUEVO (carpeta del módulo)
│   ├── MovimientoPolloEngordeService.cs                     · partial "ancla": usings, campos, ctor,
│   │                                                          constantes y helpers estáticos compartidos
│   │                                                          (EsSalidaVenta, AppendObservaciones)
│   └── Funciones/                                            ← NUEVO (funciones largas por concern)
│       ├── MovimientoPolloEngordeService.Crud.cs            · Create/Get/Search/Update/Cancel/Eliminar/
│       │                                                      Complete/CompletarBatch + ToDto + reversión
│       │                                                      de inventario + mapeo de errores de BD
│       ├── MovimientoPolloEngordeService.ResumenDisponibilidad.cs · resumen de aves + disponibilidad
│       ├── MovimientoPolloEngordeService.Auditoria.cs       · AuditarVentas + CorregirVentasCompletadas
│       ├── MovimientoPolloEngordeService.VentaGranja.cs     · CreateVentaGranjaDespacho
│       └── MovimientoPolloEngordeService.OrganizarPeso.cs   · OrganizarPeso
└── MovimientoPolloEngordeFilterDataService.cs               (sin cambios; fuera de alcance)
```

- **Namespace:** todos los archivos mantienen `namespace ZooSanMarino.Infrastructure.Services;`
  (NO folder-based) para no romper referencias/DI. La carpeta es solo organización física.
- El `.csproj` es SDK-style con globbing → los archivos nuevos se incluyen solos.
- Se **elimina** el archivo original `Services/MovimientoPolloEngordeService.cs` (su contenido queda
  repartido en los nuevos).

### Reparto de miembros (cada miembro en EXACTAMENTE un archivo)
- **Ancla (main):** campos `_ctx`/`_currentUser`, ctor, `MaxObservacionesLen`, `EsSalidaVenta`,
  `AppendObservaciones`.
- **Crud.cs:** `CreateAsync`, `ValidarDisponibilidadParaCrearAsync`,
  `RellenarOrigenDesdeLoteOrigenSiFaltaAsync`, `GetByIdAsync`, `ToDto`, `GetAllAsync`, `SearchAsync`,
  `UpdateAsync`, `CancelAsync`, `EliminarAsync`, `RollbackIfNeededAsync`,
  `MapDbUpdateToInvalidOperation`, `MensajeAyudaCorreccionCompletados`,
  `EnsureLotesCargadosParaRevertirAsync`, `ValidarLotesPresentesParaRevertir`,
  `RevertirEfectoCompletadoEnLotes`, `CompleteAsync`, `CompletarBatchAsync`.
- **ResumenDisponibilidad.cs:** `GetResumenAvesLoteAsync`, `LoadResumenAvesLoteAveEngordeBatchAsync`,
  `LoadResumenAvesLoteReproductoraBatchAsync`, `GetResumenAvesLotesAsync`,
  `GetAvesDisponiblesLotesAsync`.
- **Auditoria.cs:** `AuditarVentasEngordeAsync`, `CorregirVentasCompletadasAsync`.
- **VentaGranja.cs:** `CreateVentaGranjaDespachoAsync`.
- **OrganizarPeso.cs:** `OrganizarPesoAsync`.

> `MapDbUpdateToInvalidOperation`/`AppendObservaciones` son cross-concern (CRUD + Auditoría); al ser
> `partial class` siguen accesibles desde el archivo de Auditoría aunque se declaren en otro archivo.

---

## 4. Método mecánico (seguro)

- Script Python que parte el archivo por **bloques contiguos de métodos**, anclando los cortes a la
  línea del doc-comment (`///`) que precede a cada método (para no separar el comentario de su
  método).
- **Invariante asertado:** la unión de los rangos extraídos == región de miembros original (16..1902)
  sin solapes ni huecos → ninguna línea de código se pierde ni se duplica.
- Cada archivo `partial`: copia del bloque de `using` (9 líneas) + `namespace …;` +
  `public partial class MovimientoPolloEngordeService { … }`. La interfaz `: IMovimientoPolloEngordeService`
  va SOLO en el archivo ancla.

---

## 5. Validación
1. `dotnet build` backend → 0 errores (mismas advertencias preexistentes).
2. Revisar que la firma pública (métodos de la interfaz) no cambió.
3. No hay BD local/psql aquí; el refactor no toca schema ni datos → no requiere migración.
4. Sin procesos vivos tras validar.

---

## 6. Follow-up opcional (fuera de alcance)
- Extraer math puro a `Application/Calculos/MovimientoPolloEngordeCalculos.cs` (convención existente):
  prorrateo de peso (espejo de `prorateo-peso.funcion.ts` del front), cálculo de exceso/límite
  vendible de la auditoría. Mejora testabilidad; se hace aparte por requerir separar lógica de BD.
- Mover `MovimientoPolloEngordeFilterDataService.cs` a la carpeta del módulo por cohesión.
