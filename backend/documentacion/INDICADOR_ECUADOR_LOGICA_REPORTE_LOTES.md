# Indicador Ecuador – Lógica del reporte (solo Lote Ave Engorde, fechas y tabs)

Documento para **validar la lógica** antes de implementar cambios en backend y en `indicador-ecuador` (frontend).

---

## 1. Situación actual (importante)

| Pieza | Qué usa hoy |
|-------|-------------|
| **Vista “general”** (`/calcular`, `/consolidado`, **GET `/lotes-cerrados`**, `/liquidacion-periodo`) | Tabla **`lotes`** (Levante/Producción), `movimiento_aves`, seguimientos de levante/producción/reproductora **del modelo viejo**. **No** es `lote_ave_engorde`. |
| **Vista Pollo Engorde (Ecuador)** | Catálogo vía **`LoteReproductoraAveEngorde/filter-data`** (trae granjas + `lotesAveEngorde`). Cálculo: **POST** `indicadores-pollo-engorde-por-lote-padre` → devuelve **lote padre** + **lista de reproductores** (tabs padre + reproductores). |

**Brecha:** Si el negocio pide “lotes cerrados por rango de fechas” **en pollo engorde**, hoy **GET `lotes-cerrados`** sigue el modelo **`lotes`**, no `LoteAveEngorde`. Hay que **definir un criterio nuevo** sobre `lote_ave_engorde` (y fechas de encaset / cierre desde `movimiento_pollo_engorde` o la lógica ya existente en `IndicadorEcuadorService` para AE).

---

## 2. Objetivos acordados (requerimiento)

1. **No trabajar por “lote reproductora” como entidad seleccionable** en este reporte: solo el **lote** (**Lote Ave Engorde** / lote padre).
2. **Filtrar por `fecha inicio` y `fecha fin`** y obtener **todos los lotes (AE)** que:
   - entren en la **franja de encasetamiento**, **o**
   - estén **cerrados** de forma coherente con esa franja (ver §3).
3. **Presentación:** **tabs dinámicos**, uno por **cada lote disponible** según el filtro, mostrando el indicador / detalle de ese lote (solo padre; **sin** tabs hijos por reproductor, o con reproductores **ocultos / no cargados**).

---

## 3. Criterio de inclusión de un lote (Lote Ave Engorde) — **a validar con negocio**

Se propone comparar **fechas en calendario** (inicio del día / fin del día en zona horaria acordada, p. ej. local Ecuador o UTC según ya use el sistema).

**Datos por lote padre:**

- `FechaEncaset` → `lote_ave_engorde.fecha_encaset` (o equivalente).
- `LoteCerrado` → misma regla que ya usa `CalcularIndicadorLoteAveEngordeAsync` (aves actuales = 0 o cierre “vía reproductores”, etc.).
- `FechaCierreLote` → última fecha de movimientos Venta/Despacho/Retiro sobre **ese** `lote_ave_engorde` (ya implementado en `FechaCierrePolloEngordeAsync` para AE).

### Opción A — Unión (recomendada para “operación + cierres del período”)

Incluir el lote **L** si se cumple **al menos una** condición:

1. **Encaset en ventana:**  
   `FechaEncaset(L)` ∈ `[fechaInicio, fechaFin]` (inclusive).
2. **Cierre en ventana (solo si está cerrado):**  
   `LoteCerrado(L) == true` **y** `FechaCierreLote(L)` ∈ `[fechaInicio, fechaFin]`.

*Efecto:* entran lotes que **empezaron** en el mes (aunque sigan abiertos) **y** lotes que **cerraron** en el mes (aunque el encaset haya sido antes). No entra un lote abierto con encaset fuera del rango.

### Opción B — Solo cerrados en rango

Solo incluir si `LoteCerrado` y `FechaCierreLote` ∈ `[fechaInicio, fechaFin]` (similar a **liquidación por período** ya existente para el modelo `lotes`, pero aplicado a AE).

### Opción C — Solo encaset en rango

Solo `FechaEncaset` ∈ `[fechaInicio, fechaFin]` (ignora cierre para el filtro).

**Preguntas para cerrar:**

- ¿Los lotes **abiertos** con encaset dentro del rango deben verse siempre (Opción A)?
- ¿Un lote cerrado con encaset **fuera** del rango pero cierre **dentro** debe incluirse? (Opción A = sí; Opción C = no.)

---

## 4. Alcance “solo lote”, sin reproductora

| Antes | Después (objetivo) |
|-------|---------------------|
| Catálogo desde endpoint de **Reproductora/filter-data** | Preferir catálogo que exponga **solo** lotes AE (p. ej. `LoteAveEngorde/form-data` o endpoint dedicado **sin** listar reproductoras como ítems de selección). |
| **POST** `indicadores-pollo-engorde-por-lote-padre` devuelve padre + `lotesReproductores` | Para esta pantalla: o **nuevo flag** `incluirReproductores: false` que omita cálculo/lista de reproductores, o **nuevo endpoint** liviano “solo indicador padre” para cada tab. |

Los indicadores **numéricos del padre** no requieren mostrar tabs de reproductor; la lógica interna (traslados a reproductores, cierre “todo vendido en hijos”) puede seguir igual **solo para determinar** `LoteCerrado` del padre.

---

## 5. Tabs dinámicos (UI)

1. Usuario elige **fecha inicio**, **fecha fin**, (opcional) **granja** / filtros heredados.
2. Backend devuelve **lista ordenada** de lotes AE que cumplen §3 (ids + nombre + granja + fechas + bandera cerrado).
3. Frontend genera **un tab por lote** (etiqueta sugerida: `LoteNombre` o `LoteNombre · Granja`).
4. Al activar un tab (o en paralelo con límite de concurrencia), se pide el **indicador solo del lote padre** (sin reproductores).

**Rendimiento:** si hay muchos lotes, valorar: carga **lazy** al seleccionar el tab, o lote máximo de tabs visibles + “Ver más”.

---

## 6. Pasos de implementación sugeridos (después de validar)

1. **Backend:** endpoint tipo `POST IndicadorEcuador/lotes-ave-engorde-para-reporte` con `{ fechaInicio, fechaFin, granjaId? }` que devuelva la lista según **Opción A/B/C** acordada.
2. Ajustar o añadir cálculo reutilizando `CalcularIndicadorLoteAveEngordeAsync` / consultas existentes; **no** duplicar reglas de cierre inconsistentes.
3. **Frontend:** dejar de depender de `LoteReproductoraAveEngorde/filter-data` para el catálogo de esta vista; alinear con el nuevo listado por fechas + tabs.
4. Pruebas: casos borde (lote abierto en rango, lote cerrado cierre en rango encaset fuera, sin movimientos, zona horaria).

---

## 7. Checklist de validación (negocio / PO)

- [ ] ¿Opción A, B o C para el filtro?
- [ ] ¿Granja obligatoria o puede ser “todas las asignadas al usuario”?
- [ ] ¿Incluir lotes con `FechaEncaset` nula?
- [ ] ¿Límite máximo de lotes por consulta?
- [ ] ¿Tabs con prefetch de todos los indicadores o solo el tab activo?

---

*Última actualización: documento de diseño; implementación pendiente de aprobación de la lógica en §3.*
