# App móvil — consumo de ítems en piloto + traslado de huevos

> **Estado:** plan · 23-ago-2026
> **Origen:** auditoría de alcance (5 lectores sobre backend y front web).
> **Decisiones del usuario:** gestión de inventario → **web**; en el móvil van **ventas y
> movimientos**; se enciende el **consumo con ítems** en una empresa piloto; el primer flujo nuevo
> es **traslado de huevos**.

---

## 0. Qué se decidió y por qué

La pregunta era si la app ya alcanzaba para gestionar postura, engorde e inventario. **No.** La
auditoría midió que la app es un capturador de seguimiento diario de una sola vía —solo crea, no
edita ni borra— y que **cero** de las cinco operaciones pedidas existen en el móvil.

Reparto acordado:

| Va al **móvil** | Va a la **web** |
|---|---|
| Consumo de alimento con ítems (ya construido, apagado) | Ajuste y eliminación de stock |
| Traslado de huevos | Anulación de movimientos históricos |
| Ventas: peso de despacho | Edición de fechas de ingreso/traslado |
| (más adelante) recepción de tránsito, traslado de aves | Kardex, cuadre, CRUD de catálogos |

El criterio no es el módulo sino **dónde nace el dato**: lo que el operario cuenta con las manos va
al galpón; las correcciones contables con motivo auditado y tablas de 8 filtros, no.

---

## A · Encender el consumo con ítems en una empresa piloto

**Es el mayor valor por menor superficie de todo el backlog: cero código nuevo.**

Hoy la app manda `consumoKgHembras` como escalar suelto ⇒ el backend arma `Metadata` en null ⇒ el
gate de consumo no corre ⇒ **el stock del galpón no se mueve**. El selector de ítems ya está
construido (`selector_items_inventario.dart`, `items_consumo.dart`), gateado por
`companies.descuenta_inventario_desde_movil`, apagado en toda empresa.

### Requisito de la piloto

- **No puede manejar inventario por silo.** `SelectorItemsInventario` pasa `manejaSilos: false`
  fijo (decisión F5.5). Con silos, el backend rechaza cada ítem.
- Tiene que tener catálogo y existencias cargadas, o el selector aparece vacío.

### Verificación (la parte que importa)

1. `GET /api/CuadreAlimentoEngorde` **antes** — congela la línea base.
2. Cargar un día desde la app con ítems reales.
3. Comprobar: el movimiento de inventario existe, el stock bajó exactamente lo cargado, y
   `historicoConsumoAlimento` tiene la fila.
4. `GET /api/CuadreAlimentoEngorde` **después** — el invariante por galpón no se movió de 0
   descuadrados. **Si se movió, es una regresión**, no un efecto esperado.
5. Smoke doble: una empresa con el flag **OFF** tiene que quedar byte a byte igual.

> ⚠️ Encender el flag en **producción** es un cambio de datos en `companies` de un cliente real.
> Acá se verifica en local; el encendido en prod se hace con OK explícito y por el camino que
> corresponda (migración data-only idempotente o acción de admin), nunca a mano contra RDS.

---

## B · Traslado de huevos

El más limpio de los cinco flujos: un endpoint, un DTO, disponibilidad consultable antes de
capturar, y el operario cuenta con las mismas 11 categorías que ya tipea en el seguimiento de
producción. El trabajo de Flutter es acotado (~1 semana). **El costo está antes, en el backend.**

### B.1 — Dos fixes de backend, innegociables

Los dos son defectos reales **hoy**, también para el web. No son "preparar el terreno para el móvil":
son bugs que el móvil amplificaría.

**1. `numero_traslado` es UNIQUE y se inserta vacío.**
`TrasladoHuevosService.cs:213-216` graba con `string.Empty` y recién en un segundo `SaveChanges`
escribe `HUE-…`. Dos creates concurrentes chocan en `''`. Una cola offline que sube 5 traslados
seguidos al recuperar señal es **el caso exacto** que lo dispara.
→ La numeración va **en la misma transacción** que el insert.

**2. `ProcesarTrasladoAsync` se traga toda excepción y devuelve `false` en silencio**
(`catch { return false; }`, :254-258), pero el POST responde **201 con el DTO igual**.
→ Es el único modo de fallo que un cliente offline **no puede detectar nunca**: la app diría
"traslado registrado" con los huevos sin descontar. El 201 tiene que dejar de mentir.

Ambos con test. ~3-4 días.

### B.2 — Una decisión de producto (media hora, pero bloqueante)

**Qué manda la app en `granjaDestinoId` / `tipoDestino`.** Hoy los dos formularios del web se
contradicen: el standalone exige `granjaDestinoId` en Traslado; el modal lo manda `undefined` y
fuerza `tipoDestino='Planta'`. Y **el reporte contable clasifica Entrada/Salida por ese mismo
campo** ⇒ el mismo traslado sale "Salida" desde un form y "Entrada" desde el otro.

Queda documentado sin resolver en el plan original (§9.3). Si la app elige mal, ensucia el contable.
**Se resuelve antes de escribir la pantalla, no después.**

### B.3 — Lo que la app NO va a hacer en esta fase

- **Traslado entre granjas.** El flujo es **unilateral**: `GranjaDestinoId`/`LoteDestinoId` son
  metadatos puros y **nada acredita los huevos en el destino**. Un "traslado entre granjas" los hace
  desaparecer del sistema. Sin recepción ni tránsito, la app solo ofrece el destino que el negocio ya
  usa de verdad (galpón → planta).
- **Santa Reyes (`clasificacion_huevo_por_items`).** Exige el array `huevoItems`; la app maneja las
  11 columnas fijas. Con ese flag encendido el traslado no descontaría por ítem ⇒ **la pantalla se
  gatea por empresa** hasta que el móvil soporte ítems de huevo.

### B.4 — Flutter

- Tipo de cola `movimiento-huevos` — **ya existe** en el modelo y la pantalla de sincronización
  ya sabe rotularlo. Falta el productor.
- ⚠️ La cola deduplica por PK `(modulo, lote_id, fecha)` en `registros_conocidos`. **Un traslado es
  N-por-día, no 1**: hay que sacar estos tipos de esa heurística o la app descartará traslados
  legítimos como duplicados.
- Disponibilidad: `GET /api/traslados/lote-lpp/{id}/disponibilidad` **antes** de capturar, para que
  el operario no cargue más de lo que hay.

---

## C · Riesgos transversales

- **Tres de los cinco flujos auditados devuelven 201 cuando el procesamiento falló en silencio.**
  Es el modo de fallo que rompe cualquier cliente offline. B.1#2 arregla el de huevos; los otros
  quedan anotados.
- **Cero tests** de `TrasladoHuevosService`, `DisponibilidadLoteService` y
  `EspejoHuevoProduccionSyncService` — donde vive toda la lógica de stock de huevos.
- La app **no puede corregir** un registro (sin PUT/DELETE). Antes de que escriba movimientos de
  stock conviene resolverlo: si no puede corregir un seguimiento, mucho menos deshacer un traslado.

---

## D · Verificación

| Compuerta | Comando | Criterio |
|---|---|---|
| Backend | `dotnet build` + `dotnet test` | 0 errores, tests nuevos verdes |
| App | `flutter analyze` + `flutter test` | 0 errores; sin infos nuevos |
| Cuadre alimento | `GET /api/CuadreAlimentoEngorde` antes/después | no se mueve de 0 descuadrados |
| Smoke doble | empresa flag OFF / flag ON | la OFF, byte a byte igual |
| Concurrencia | 5 traslados en ráfaga | 5 números distintos, 0 colisiones |
