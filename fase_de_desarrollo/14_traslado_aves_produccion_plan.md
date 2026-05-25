# Feature 14 — Traslado de Aves en Producción (paridad con Levante)

**Fase:** Replicar end-to-end la lógica del Feature 13 (Levante) en el módulo
de **Seguimiento Diario Producción**.
**Inicio:** 2026-05-25
**Antecedente:** Feature 13 completa — `fase_de_desarrollo/13_traslado_aves_mejorado_plan.md`

---

## 🎯 Objetivos

1. **Modal de traslado** ya soporta Producción↔Producción (radio fijo según origen).
2. **Tabla `produccion_seguimiento` enriquecida** con columnas dedicadas:
   - `traslado_ingreso_hembras`, `traslado_ingreso_machos`
   - `traslado_salida_hembras`, `traslado_salida_machos`
   - `es_traslado`, `traslado_lote_contraparte_id`, `traslado_granja_contraparte_id`, `traslado_direccion`
   - `updated_by_user_id` (auditoría)
3. **Tabla `lote_postura_produccion`** con acumulados:
   - `traslado_ingreso_hembras/machos`
   - `traslado_salida_hembras/machos`
4. **Backend**: `TrasladoAvesDesdeSegService.EjecutarTrasladoDesdeSegAsync` ya cubre Producción — sólo necesita escribir en `produccion_seguimiento` con la nueva lógica (UPSERT + columnas dedicadas).
5. **CreateAsync de SeguimientoProduccionService**: MERGE si existe fila sólo-traslado en la misma fecha; bloqueo si tiene datos manuales.
6. **DeleteAsync**: revierte traslado en ambos lados (LPP origen y destino, fila contraparte).
7. **Descuento centralizado**: mortalidad + selección + error de sexaje (si aplica) descuentan `AvesHActual`/`AvesMActual` en LPP. Hoy el servicio no lo hace — hay que añadirlo.
8. **Frontend** — tabla del seguimiento producción:
   - 4 columnas dedicadas de traslado.
   - Fila amarilla para traslados.
   - Modal de confirmación detallado al eliminar.
9. **Información del lote** de producción: 4 mini-cards con acumulados.
10. **Excel descarga**: misma cabecera detallada del Feature 13.

---

## 🗄️ Cambios DB

### Script `backend/sql/050_add_traslado_acumulados_lote_postura_produccion.sql`
4 columnas en LPP, default 0.

### Script `backend/sql/051_add_traslado_columns_produccion_seguimiento.sql`
- 4 columnas split H/M en `produccion_seguimiento`.
- Columnas `es_traslado`, `traslado_lote_contraparte_id`, `traslado_granja_contraparte_id`, `traslado_direccion` con CHECK constraint.
- `updated_by_user_id`.
- Índice parcial por `es_traslado`.

### Migración EF Core (idempotente)
`AddTrasladoAcumuladosLPP` (cubre ambos scripts).

---

## 🔧 Cambios Backend

### Dominio (`Domain/Entities/SeguimientoProduccion.cs`)
- Añadir 4 properties traslado H/M dedicadas.
- Añadir `EsTraslado`, `TrasladoLoteContraparteId`, `TrasladoGranjaContraparteId`, `TrasladoDireccion`.
- Añadir `UpdatedByUserId`, `UpdatedAt`.

### Dominio (`Domain/Entities/LotePosturaProduccion.cs`)
- Añadir 4 properties acumulado traslado.

### EF Configuration
- `SeguimientoProduccionConfiguration.cs`: mapear nuevas columnas + índice.
- `LotePosturaProduccionConfiguration.cs`: mapear acumulados con default 0.

### DTOs
- `SeguimientoProduccionDto`: añadir nuevas columnas.
- `CreateSeguimientoProduccionDto`: añadir mortalidad fields como antes.
- `LotePosturaProduccionDto`: añadir acumulados.

### Servicios
- `TrasladoAvesDesdeSegService` (rama Producción):
  - Reemplazar el UPSERT actual (legacy: MortalidadH/M usado como traslado) por la misma lógica de Levante:
    - SD origen: `TrasladoSalidaHembras/Machos += dto.TrasladoHembras/Machos`.
    - SD destino: `TrasladoIngresoHembras/Machos += dto.TrasladoHembras/Machos`.
    - Flag `EsTraslado=true`, contraparte, dirección.
  - LPP origen: `TrasladoSalidaHembras/Machos +=`, `AvesHActual/MActual -=`.
  - LPP destino: `TrasladoIngresoHembras/Machos +=`, `AvesHActual/MActual +=`.

- `SeguimientoProduccionService.CreateAsync`:
  - Detectar fila pre-existente para misma `(LoteId, Fecha)`:
    - Si sólo tiene traslado (sin mortalidad/sel/consumo) → MERGE.
    - Si tiene manual → InvalidOperationException.
    - Si no hay fila → INSERT normal.
  - Aplicar descuento de aves en LPP centralizado (helper `AplicarDescuentoLppAsync` ya existe en `SeguimientoDiarioService` para LPP — verificar si se reutiliza o se crea uno local).

- `SeguimientoProduccionService.DeleteAsync`:
  - Detectar `traslado_*` > 0 → reversión completa de ambos lados.
  - Si NO es traslado → simplemente borrar + devolver mortalidad/sel a LPP.

### Resumen-mortalidad de Producción
- Si existe endpoint análogo a `GetMortalidadResumenAsync` para producción, incluir traslados acumulados.
- Si no existe, crear `GetMortalidadResumenProduccionAsync` que devuelva igual estructura adaptada.

---

## 🎨 Cambios Frontend

### Servicios
- `seguimiento-produccion.service.ts` (o equivalente): añadir nuevos campos al DTO.
- `lote-postura-produccion.service.ts`: añadir 4 campos acumulados.

### Tabla seguimiento producción
- Componente `lote-produccion-list` (o el componente de tabla de registros):
  - 4 columnas: ↘ Ing. hembras / machos, ↗ Sal. hembras / machos.
  - Headers verde / ámbar (igual que Levante).
  - Fila amarilla si tiene traslado.
  - Saldo aves vivas con traslado en cálculo.

### Información del lote producción
- 4 mini-cards: Ingreso H/M, Salida H/M (mismo patrón que Levante).

### Modal de confirmación de delete
- Reutilizar `ConfirmationModalComponent` con mensaje detallado para traslado.

### Excel descarga
- Cabecera con info detallada (Granja, Núcleo, Galpón, Fase Producción, Fechas, Aves vivas H/M, Mortalidad/Selección acum, Traslados acum) + columnas de traslado.

---

## 🧪 Casos de prueba

1. Traslado Producción → Producción (mismo lote-base): saldo origen baja, destino sube.
2. Intento Producción → Levante: backend rechaza por mismo-tipo (regla del Feature 13).
3. Seguimiento manual sobre fecha de traslado en Producción: MERGE (preserva traslado, añade mortalidad).
4. Eliminar traslado: revierte ambos lados.
5. Eliminar seguimiento manual sobre traslado: solo revierte parte manual; los datos de traslado quedan.
6. Excel descarga de producción muestra cabecera + 4 columnas + auditoría.
