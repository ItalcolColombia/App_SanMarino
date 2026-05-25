# Feature 13 — Traslado de Aves Mejorado (Levante)

**Fase:** Mejora del flujo de traslado de aves desde el módulo Seguimiento Diario Levante.
**Módulo siguiente (no en este sprint):** Aplicar la misma lógica a Producción.
**Inicio:** 2026-05-24

---

## 🎯 Objetivos del cambio

1. **Restricción por etapa**: Desde Levante, sólo se permite trasladar a otro lote **en Levante** (hasta semana 25). El radio button "Producción" se OCULTA en el módulo de seguimiento levante.
2. **Inventario real (no encasetamiento)**: El modal muestra las **aves vivas actuales** (saldo después de seguimientos), no las iniciales. Misma fuente que `/api/Lote/{id}/resumen-mortalidad`.
3. **Registro como seguimiento independiente**: Cada traslado crea un **nuevo registro** en `seguimiento_diario` (tipo `levante`) con bandera `es_traslado=true`. Se renderiza en la tabla con **fondo amarillo** para toda la fila.
4. **Campos acumulativos en `lote_postura_levante`**:
   - `traslado_ingreso_hembras`, `traslado_ingreso_machos` — aves recibidas (suma de todos los traslados entrantes).
   - `traslado_salida_hembras`, `traslado_salida_machos` — aves enviadas (suma de todos los traslados salientes).
5. **Saldo real con traslados**:
   `saldoH = HembrasL - MortCajaH - MortAcumH - SelH - ErrH - TrasladoSalidaH + TrasladoIngresoH`
   Igual para machos.
6. **Eliminación de traslado**: Borrar un registro de tipo traslado **revierte** el inventario en ambos lados (origen recupera, destino pierde).
7. **Visualización**:
   - "Información del lote" en seguimiento diario: añadir 4 mini-cards (ingreso H/M y salida H/M).
   - Vista detalle del lote (LotePosturaLevante): mostrar mismos 4 totales.

---

## 🗄️ Cambios en la base de datos

### Script `backend/sql/046_add_traslado_acumulados_lote_postura_levante.sql`

```sql
ALTER TABLE lote_postura_levante
  ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;
```

### Script `backend/sql/047_add_es_traslado_to_seguimiento_diario.sql`

```sql
ALTER TABLE seguimiento_diario
  ADD COLUMN IF NOT EXISTS es_traslado BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id INTEGER NULL,
  ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id INTEGER NULL,
  ADD COLUMN IF NOT EXISTS traslado_direccion VARCHAR(10) NULL; -- 'SALIDA' | 'INGRESO'

CREATE INDEX IF NOT EXISTS idx_seguimiento_diario_es_traslado ON seguimiento_diario(es_traslado);
```

> Nota: tras ejecutar los SQL, generar migraciones EF Core con `dotnet ef migrations add AddTrasladoAcumuladosLPL` y `AddEsTrasladoSeguimientoDiario` para mantener el snapshot alineado.

---

## 🔧 Cambios Backend

### Dominio
- `LotePosturaLevante.cs`: añadir `TrasladoIngresoHembras`, `TrasladoIngresoMachos`, `TrasladoSalidaHembras`, `TrasladoSalidaMachos` (int con default 0).
- `SeguimientoDiario.cs`: añadir `EsTraslado` (bool), `TrasladoLoteContraparteId` (int?), `TrasladoGranjaContraparteId` (int?), `TrasladoDireccion` (string?).

### EF Configuration
- `LotePosturaLevanteConfiguration.cs`: mapear nuevas columnas con default 0.
- `SeguimientoDiarioConfiguration.cs`: mapear `es_traslado` con default false e índice.

### DTOs
- `LotePosturaLevanteDto`: añadir los 4 nuevos campos.
- `LoteMortalidadResumenDto`: añadir `TrasladoIngresoHembras`, `TrasladoIngresoMachos`, `TrasladoSalidaHembras`, `TrasladoSalidaMachos` para mostrarlos en frontend.
- `TrasladoAvesDesdeSegDiarioDto`: añadir `AvesHDisponiblesReal`, `AvesMDisponiblesReal` (opcionales, para validar en backend con el saldo real).

### LoteService.GetMortalidadResumenAsync
- Sumar los nuevos campos al cálculo de saldo:
  ```csharp
  // Tomar traslados acumulados desde LPL (si existe) — o calcular desde seguimiento_diario
  var lpl = await _ctx.LotePosturaLevante.AsNoTracking()
      .Where(l => l.LoteId == loteId && l.DeletedAt == null)
      .FirstOrDefaultAsync();
  int trasladoInH = lpl?.TrasladoIngresoHembras ?? 0;
  int trasladoOutH = lpl?.TrasladoSalidaHembras ?? 0;
  // ... mismo para machos
  int saldoH = Math.Max(0, baseH - mortCajaH - mortH - selH - errH + trasladoInH - trasladoOutH);
  int saldoM = Math.Max(0, baseM - mortCajaM - mortM - selM - errM + trasladoInM - trasladoOutM);
  ```
- Devolver los 4 totales en el DTO.

### TrasladoAvesDesdeSegService.EjecutarTrasladoDesdeSegAsync (REESCRIBIR)
- **Validación de etapa**: si `TipoOrigen == "Levante"` entonces `TipoDestino` debe ser `"Levante"` (rechazar si difiere).
- **Validación de stock**: usar saldo real con `GetMortalidadResumenAsync(origenLoteBaseId)` (sumar traslados), NO `AvesHActual` directo.
- **Crear NUEVO registro en `seguimiento_diario`** (no UPSERT):
  ```csharp
  var segSalida = new SeguimientoDiario {
    TipoSeguimiento = "levante",
    LoteId = loteOrigenBaseId.ToString(),
    LotePosturaLevanteId = origenLPL.LotePosturaLevanteId,
    Fecha = fechaDate,
    EsTraslado = true,
    TrasladoDireccion = "SALIDA",
    TrasladoLoteContraparteId = destinoLPL.LotePosturaLevanteId,
    TrasladoGranjaContraparteId = destinoLPL.GranjaId,
    MortalidadHembras = dto.TrasladoHembras,  // se cuentan como descuento para saldo
    MortalidadMachos = dto.TrasladoMachos,
    Observaciones = $"Traslado SALIDA a lote {destinoLPL.LoteNombre}: {dto.Observaciones}",
    Ciclo = "Traslado",
    CreatedByUserId = usuarioId.ToString(),
    CreatedAt = DateTime.UtcNow
  };
  ```
  > **Decisión técnica**: usamos `MortalidadHembras/Machos` como "descuento" del saldo (sin afectar la mortalidad acumulada cuando se filtre por `es_traslado=false`). Para el indicador real, `GetMortalidadResumenAsync` filtrará `WHERE es_traslado=false`.
- **Crear registro INGRESO** en seguimiento_diario para el lote destino (mismo patrón, dirección INGRESO, MortalidadH/M = -dto.Traslado** No: usamos campos separados** — mejor enfoque: agregar campos al SD).

### REFINAMIENTO: usar campos dedicados, no mortalidad
- Reinterpretar: el registro SD del traslado lleva `MortalidadHembras=0` etc., pero sí lleva `TrasladoAvesEntrante`/`TrasladoAvesSalida` (campos que ya existen en `seguimiento_diario`). El descuento real se aplica en `lote_postura_levante.traslado_salida_hembras` y el saldo se calcula desde ahí.

### Actualizar `TrasladoAvesDesdeSegService` — versión final
1. Inicio transacción
2. Validar mismo tipo (levante ↔ levante) ✋
3. Validar stock real con saldo = saldoH/saldoM
4. **Origen**: `LPL.TrasladoSalidaHembras += dto.TrasladoHembras`, idem machos. Decrementar también `AvesHActual/AvesMActual`.
5. **Destino**: `LPL.TrasladoIngresoHembras += dto.TrasladoHembras`, idem machos. Incrementar `AvesHActual/AvesMActual`.
6. **Crear SD SALIDA** en seguimiento_diario con `EsTraslado=true`, `TrasladoDireccion='SALIDA'`, `TrasladoAvesSalida=dto.TrasladoHembras+dto.TrasladoMachos`, `TrasladoLoteContraparteId`, `TrasladoGranjaContraparteId`.
7. **Crear SD INGRESO** en seguimiento_diario para el destino con `EsTraslado=true`, `TrasladoDireccion='INGRESO'`, `TrasladoAvesEntrante=dto.TrasladoHembras+dto.TrasladoMachos`.
8. Insertar `MovimientoAves` para auditoría (ya existe).
9. Commit.

### SeguimientoLoteLevanteService.DeleteAsync — manejar reversión de traslado
- Si el registro a borrar tiene `EsTraslado=true`:
  - Si es `SALIDA`: encontrar el SD de INGRESO contraparte (`TrasladoLoteContraparteId` + misma fecha + `TrasladoDireccion='INGRESO'`) y borrar ambos.
  - Si es `INGRESO`: idem, encontrar el de SALIDA y borrar ambos.
  - Restar `traslado_salida_*` del LPL origen.
  - Restar `traslado_ingreso_*` del LPL destino.
  - Restaurar `AvesHActual/MActual` en ambos.
  - NO afectar la mortalidad acumulada (los campos no se tocan en este flujo).

### Nuevo endpoint
- `GET /api/Lote/{loteId}/aves-actuales-real` — devuelve `{ saldoH, saldoM, trasladoInH, trasladoInM, trasladoOutH, trasladoOutM }`. Reutiliza `GetMortalidadResumenAsync` extendido.

---

## 🎨 Cambios Frontend

### modal-traslado-aves-seguimiento.component.ts
- Recibir `tipoOrigen` por `@Input origen.tipoLote` (ya existe).
- **Forzar `tipoDestino = tipoOrigen` y NO mostrar radio buttons**. El radio queda fijo y oculto.
- Al abrir: llamar nuevo endpoint `GET /api/Lote/{loteIdBase}/aves-actuales-real` y mostrar saldoH/saldoM reales en la sección "Origen" (y validar con esos máximos en los inputs).
- Eliminar carga de LPP (solo LPL relevante para Levante).

### modal-traslado-aves-seguimiento.component.html
- Quitar bloque `<div class="radio-group">` con tipo destino.
- Sustituir `origen.avesHActual` por `disponibilidadReal.saldoH` (nuevo campo cargado).

### tabla-lista-registro.component.html (lista seguimientos en Levante)
- Añadir clase `fila--traslado` cuando `s.esTraslado === true`.
- Estilo amarillo en SCSS: `tr.fila--traslado { background: #fef3c7; }`.
- Mostrar columna o tag indicando "🔀 Traslado SALIDA/INGRESO" en lugar de mortalidad cuando es traslado.

### tabs-principal — Información del lote
- Añadir 4 mini-cards al lado de "Aves vivas (hembras/machos)":
  - "Ingreso traslados (H)" — `lote.trasladoIngresoHembras`
  - "Ingreso traslados (M)" — `lote.trasladoIngresoMachos`
  - "Salida traslados (H)" — `lote.trasladoSalidaHembras`
  - "Salida traslados (M)" — `lote.trasladoSalidaMachos`

### lote-list (detalle del lote postura levante)
- Añadir los 4 campos en el modal "Ver detalle" del lote.

### seguimiento-lote-levante-list.component.ts
- En `openTrasladoAvesModal()`: cargar el saldo real antes de abrir (ya pasa el `selectedLote.loteId`).
- Después de traslado exitoso: refrescar `selectedLote` y `seguimientos` y resumen.

### services
- `LoteService.getResumenMortalidad`: el DTO ya devuelve los nuevos campos.
- `TrasladosAvesService`: usar el endpoint de saldo-real-aves.

---

## 🧪 Casos de prueba

1. **Crear traslado válido (Levante→Levante)**: saldo real disminuye en origen y aumenta en destino. SD muestra fila amarilla en ambos lotes.
2. **Intento de traslado a Producción desde Levante**: el modal no muestra opción Producción (radio oculto). Backend rechaza si se manipula.
3. **Stock insuficiente**: backend rechaza con saldo real (no encasetamiento).
4. **Eliminar registro de traslado SALIDA**: las aves vuelven al origen, se descuentan del destino, se borra también el registro INGRESO contraparte.
5. **Resumen-mortalidad** devuelve saldo correcto incluyendo traslados acumulados.
6. **Información del lote** muestra los 4 acumulados.

---

## 📦 Entregables

- 2 scripts SQL (`046_*`, `047_*`).
- 2 migraciones EF Core.
- 4 cambios en Domain (`LotePosturaLevante`, `SeguimientoDiario`).
- 2 actualizaciones de Configuration EF.
- 5 DTOs actualizados o creados.
- Servicio `TrasladoAvesDesdeSegService` reescrito.
- Servicio `LoteService.GetMortalidadResumenAsync` extendido.
- Servicio `SeguimientoLoteLevanteService.DeleteAsync` con manejo de reversión.
- Nuevo endpoint `GET /api/Lote/{id}/aves-actuales-real`.
- Modal de traslado actualizado (sin Producción, con saldo real).
- Tabla seguimientos con fila amarilla para traslados.
- Información del lote con 4 mini-cards.
- Detalle lote con campos extra.
