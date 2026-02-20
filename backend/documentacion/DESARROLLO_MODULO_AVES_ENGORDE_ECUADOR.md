# Desarrollo: Módulo Seguimiento Diario Aves de Engorde (Ecuador)

Documentación de los desarrollos realizados para el módulo **Seguimiento diario pollo de engorde** en contexto Ecuador: manejo de errores al guardar, cálculo de aves disponibles con mortalidad y liquidación técnica por lote aves de engorde.

---

## 1. Modal de error al guardar registro duplicado (lote + fecha)

### Problema

Al intentar guardar **otro registro diario para el mismo lote y la misma fecha**, la base de datos rechazaba la operación por la restricción única `uq_seg_diario_aves_engorde_lote_fecha` (un solo registro por lote por día). El usuario veía un error técnico (`DbUpdateException` / `PostgresException 23505`) en lugar de un mensaje claro.

### Solución

#### Backend

- **Archivo:** `ZooSanMarino.API/Controllers/SeguimientoAvesEngordeController.cs`
- En los métodos **Create** y **Update**:
  - Se captura `DbUpdateException` cuando la excepción interna es `PostgresException` con `SqlState == "23505"` (violación de unicidad).
  - Si el nombre de la restricción es `uq_seg_diario_aves_engorde_lote_fecha`, se responde **400 Bad Request** con un mensaje amigable en el cuerpo:
    - `"Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. Solo puede haber un registro por lote por día."`
  - El resto de respuestas de error usan un objeto `{ message = "..." }` para que el frontend pueda leer `err.error.message`.

#### Frontend

- **Archivos:**  
  - `frontend/src/app/features/aves-engorde/pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component.ts`  
  - `seguimiento-aves-engorde-list.component.html`
- Se añade un **modal de error** con `ConfirmationModalComponent` (tipo `error`, título "Error al guardar", botón "Entendido").
- En el callback de **error** de `onSave` se asigna el mensaje (`err?.error?.message || err?.message` o texto por defecto) y se abre el modal.
- Al confirmar o cerrar el modal se limpia el estado (`errorModalOpen = false`).

### Restricción de base de datos

- **Tabla:** `seguimiento_diario_aves_engorde`
- **Constraint:** `uq_seg_diario_aves_engorde_lote_fecha` (un registro por `lote_ave_engorde_id` por `fecha`).

---

## 2. Aves disponibles restando mortalidad del seguimiento diario

### Problema

En la sección **"Información del lote y disponibilidad de aves"** del módulo Seguimiento diario pollo de engorde, el valor de **Aves disponibles** no disminuía cuando se registraba mortalidad (y selección / error de sexaje) en los registros de seguimiento diario. Solo se restaban las aves asignadas a lotes reproductora y la mortalidad de caja del lote.

### Solución

- **Archivo:** `ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs`
- **Método:** `GetAvesDisponiblesAsync(int loteAveEngordeId)`

Se incorpora la suma de bajas acumuladas desde **seguimiento_diario_aves_engorde** para el lote:

- **Mortalidad:** `MortalidadHembras`, `MortalidadMachos`
- **Selección:** `SelH`, `SelM`
- **Error de sexaje:** `ErrorSexajeHembras`, `ErrorSexajeMachos`

Fórmula de aves disponibles:

- **Hembras disponibles** = Hembras iniciales − Mort. caja − Asignadas a reproductoras − Mort. seguimiento − Sel H − Error sexaje H  
- **Machos disponibles** = Machos iniciales − Mort. caja − Asignadas a reproductoras − Mort. seguimiento − Sel M − Error sexaje M  

El DTO **AvesDisponiblesDto** ahora devuelve **MortalidadAcumuladaHembras** y **MortalidadAcumuladaMachos** con los totales del seguimiento diario (antes se enviaban en 0).

### Frontend

- Texto de ayuda bajo el total de aves disponibles actualizado para indicar que también se descuenta *"la mortalidad/selección registrada en los seguimientos diarios"*.

### Tablas involucradas

- **lote_ave_engorde:** datos iniciales del lote (hembras, machos, mort. caja).
- **lote_reproductora_ave_engorde:** aves asignadas a lotes reproductora.
- **seguimiento_diario_aves_engorde:** mortalidad, selección y error de sexaje por fecha.

---

## 3. Liquidación Técnica para Ecuador (lote aves de engorde)

### Problema

El botón **"Liquidación técnica"** en el módulo **Seguimiento diario pollo de engorde** llamaba a las APIs de liquidación de **levante** (`LiquidacionTecnica`, `LiquidacionTecnicaComparacion`), que trabajan con la tabla **Lotes** (levante) y **SeguimientoLoteLevante**. El identificador que se enviaba era **LoteAveEngordeId** (por ejemplo `1`), por lo que el backend respondía *"Lote '1' no encontrado o no pertenece a la compañía"* (404/400).

### Enfoque

Se crea un **servicio y API específicos para Ecuador** que usan **lote_ave_engorde** y **seguimiento_diario_aves_engorde**, manteniendo los mismos DTOs para que el frontend no requiera otro flujo ni pantallas distintas.

### Backend

#### Interfaz y servicio

- **Interfaz:** `ZooSanMarino.Application/Interfaces/ILiquidacionTecnicaEcuadorService.cs`
- **Implementación:** `ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaEcuadorService.cs`

Métodos (todos reciben **loteAveEngordeId** y opcionalmente **fechaHasta**):

| Método | Descripción |
|--------|-------------|
| `CalcularLiquidacionAsync` | Liquidación técnica básica (mismo contrato que levante). |
| `ObtenerLiquidacionCompletaAsync` | Liquidación con detalle de seguimientos y datos de guía. |
| `CompararConGuiaGeneticaAsync` | Comparación con guía genética. |
| `ObtenerComparacionCompletaAsync` | Comparación completa con detalles y seguimientos. |
| `ValidarLoteParaLiquidacionAsync` | Indica si el lote existe y tiene seguimientos. |

Lógica interna:

- Carga del lote desde **LoteAveEngorde** (filtrado por `CompanyId` y `DeletedAt`).
- Seguimientos desde **SeguimientoDiarioAvesEngorde** (hasta `fechaHasta` y hasta semana 25 desde fecha encaset).
- Guía genética desde **ProduccionAvicolaRaw** (Raza, AnoTablaGenetica).
- Cálculo de métricas (mortalidad, selección, error sexaje, consumo, peso, uniformidad) y comparación con la guía, devolviendo los mismos DTOs que la liquidación de levante.

#### Controlador

- **Archivo:** `ZooSanMarino.API/Controllers/LiquidacionTecnicaEcuadorController.cs`
- **Ruta base:** `api/LiquidacionTecnicaEcuador`

| Método HTTP | Ruta | Descripción |
|-------------|------|-------------|
| GET | `/{loteAveEngordeId}` | Liquidación básica. |
| GET | `/{loteAveEngordeId}/completa` | Liquidación completa. |
| GET | `lote/{loteAveEngordeId}` | Comparación con guía. |
| GET | `lote/{loteAveEngordeId}/completa` | Comparación completa. |
| GET | `/{loteAveEngordeId}/validar` | Validar lote para liquidación. |

#### Registro

- En **Program.cs** se registra:  
  `ILiquidacionTecnicaEcuadorService` → `LiquidacionTecnicaEcuadorService`.

### Frontend

- **Servicios:**  
  - `LiquidacionTecnicaService`: métodos `getLiquidacionTecnica` y `getLiquidacionCompleta` aceptan un tercer parámetro **useEcuador**. Si es `true`, las peticiones van a `api/LiquidacionTecnicaEcuador`.  
  - `LiquidacionComparacionService`: `compararConGuiaGenetica` y `obtenerComparacionCompleta` con **useEcuador**; si es `true`, usan `api/LiquidacionTecnicaEcuador/lote/{id}` y `.../lote/{id}/completa`.
- **Modal:**  
  - `ModalLiquidacionComponent` tiene un **@Input() esLoteAveEngorde**. Se pasa a `app-liquidacion-tecnica` y `app-liquidacion-comparacion`.
- **Componentes de liquidación:**  
  - `LiquidacionTecnicaComponent` y `LiquidacionComparacionComponent` reciben **esLoteAveEngorde** y lo pasan a los servicios en cada llamada.
- **Módulo aves de engorde:**  
  - En **seguimiento-aves-engorde-list**, el modal de liquidación se abre con **`[esLoteAveEngorde]="true"`**, de modo que el **loteId** (que en ese contexto es **LoteAveEngordeId**) se usa contra la API Ecuador.

### Relación con documentación existente

- La definición de indicadores y tablas para pollo engorde en Ecuador se mantiene en **LIQUIDACION_TECNICA_POLLO_ENGORDE.md**.
- Este desarrollo implementa la **liquidación técnica por lote aves de engorde** (Ecuador) usando esas tablas y exponiendo una API dedicada que el frontend consume cuando el usuario abre la liquidación desde el módulo de aves de engorde.

---

## Resumen de archivos tocados

| Área | Archivos |
|------|----------|
| **Error duplicado** | `SeguimientoAvesEngordeController.cs`, `seguimiento-aves-engorde-list.component.ts/html` |
| **Aves disponibles** | `LoteReproductoraAveEngordeService.cs`, `seguimiento-aves-engorde-list.component.html` (texto) |
| **Liquidación Ecuador** | `ILiquidacionTecnicaEcuadorService.cs`, `LiquidacionTecnicaEcuadorService.cs`, `LiquidacionTecnicaEcuadorController.cs`, `Program.cs`, `liquidacion-tecnica.service.ts`, `liquidacion-comparacion.service.ts`, `modal-liquidacion.component.ts/html`, `liquidacion-tecnica.component.ts`, `liquidacion-comparacion.component.ts`, `seguimiento-aves-engorde-list.component.html` |

---

*Documento generado a partir del desarrollo del módulo Seguimiento diario aves de engorde (Ecuador).*
