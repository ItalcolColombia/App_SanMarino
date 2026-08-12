# PWA F4 — mapeo de los módulos que faltan por llevar offline

**Fecha:** 2026-08-12 · **Estado:** mapeo, **sin implementar**
**Antecedente:** F3 cubre las 4 capturas diarias
([`pwa_f3_captura_offline_plan.md`](pwa_f3_captura_offline_plan.md)).
**Auditoría que lo motiva:** [`pwa_auditoria_acceso_offline_2026-08-12.md`](pwa_auditoria_acceso_offline_2026-08-12.md).

> Este documento existe para que otra sesión retome sin volver a investigar. Todo lo de acá está
> **medido** contra el código y la BD local, no supuesto.

---

## Regla de lectura

Hoy **todos** estos módulos se **consultan** sin red (están en la lista blanca de la caché F2).
Ninguno se **guarda** sin red. Lo que sigue es qué haría falta para que se guarden.

---

## Los módulos, por dificultad creciente

### Nivel 1 — hoja: escriben su fila y poco más

| Módulo | Endpoint de alta | Qué lo complica |
|---|---|---|
| **Gastos de inventario** | `POST /api/inventario-gastos` | Casi nada. No mueve stock: registra un gasto contra lote (real o **programado**, con CHECK XOR en BD). Es el **mejor candidato para F4.1** |

**Prerrequisito real:** ninguno más allá de lo que F3 ya construyó. Necesita su tipo de operación, su
rama de despacho y verificar si el service abre transacción propia.

### Nivel 2 — mueven stock: aparece la clase (b)

| Módulo | Endpoint | Qué lo complica |
|---|---|---|
| **Gestión de inventario** (consumo/entrada) | `POST /api/inventario-gestion/...` | Toca `inventario_gestion_stock`, que ya tiene índice único y upsert atómico (A1/A2). Dos dispositivos consumiendo el mismo ítem en paralelo ⇒ **stock insuficiente al sincronizar** |
| **Inventario de aves** | `POST /api/InventarioAves` | Ídem sobre saldos de aves |

🔴 **Bloqueante compartido:** la clase **(b) divergencia con el mundo** del plan madre está
**modelada y sin emisor**. Hoy `SyncPushCalculos.Estados.RequiereCuadre` existe y nadie lo emite. Sin
eso, un «no hay stock» al sincronizar se rechaza como error de captura — y **perder el dato de campo
es peor que un saldo temporalmente negativo**. Hay que:

1. Que el service distinga «faltó stock» de «el dato está mal».
2. Aplicar igual, marcando `requiere_cuadre`, y generar la tarea para el supervisor.
3. Emitirlo desde el push y que el cliente lo trate como confirmado (ya lo hace:
   `clasificarResultadoPush` devuelve `borrar` para `requiere_cuadre`).

### Nivel 3 — operaciones de dos lados

| Módulo | Endpoint | Qué lo complica |
|---|---|---|
| **Movimiento de aves** | `POST /api/MovimientoAves` | Origen y destino. Encolar un solo lado deja el otro sin contraparte |
| **Movimiento pollo engorde** (+ Panamá) | `POST /api/MovimientoPolloEngorde`, `.../Panama` | Ídem |
| **Traslados** | `POST /api/traslados` | Recepción de tránsito: **N entradas por `TransferGroupId`** — nunca asumir una |
| **Huevos** (traslado/clasificación) | dentro de producción y `traslado_huevos` | El espejo lo llena un trigger; el histórico unificado se **anula**, no se borra |
| **Venta de aves** | vía movimientos | Además toca cliente y, en Panamá, **peso báscula diferido** |

🔴 **Bloqueante compartido:** el **grafo de operaciones** con `client_entity_id`. El plan madre lo
describe: columna `uuid UNIQUE` nullable poblada solo por capturas offline, el push manda el grafo
como unidad, el servidor resuelve las referencias dentro de la misma transacción y devuelve el mapa
`uuid → id`. **Sin ids negativos**, y el cliente nunca reescribe referencias a posteriori.

---

## Lo que hay que hacer ANTES de cualquiera de estos

| # | Ítem | Por qué bloquea |
|---|---|---|
| **B1** | `jti` + `sesiones_activas` + refresh token | Hoy **no hay forma de revocar una sesión**. Una jornada offline de 16 h sin revocación es una ventana de acceso irrevocable. Es el más urgente |
| **A4** | El self-heal de `aves_*_actual` al patrón aplicador | Un `GET` que escribe inviabiliza cualquier cursor de sync |
| **B8** | Rotar las 4 llaves de `environment.prod.ts` | Están en texto plano y quemadas en git. **Las llaves las genera el usuario** |
| **B10** | Super admin por email → a datos | Atraviesa el aislamiento multiempresa y no se revoca sin deploy |
| **B5/B6** | Completar autor y fallback de empresa fuera del camino de sync | En el push ya están; en el tráfico normal no |

---

## Patrón a copiar (ya probado en F3)

1. Tipo nuevo en `SyncPushCalculos.Tipos` + entrada en `Tipos.Todos`.
2. Rama de despacho en un partial de `SyncPushService`, llamando **al mismo service que usa el
   controller** — nunca reimplementar reglas.
3. Si el service abre transacción propia ⇒ hacerla **condicional**
   (`CurrentTransaction is null ? Begin() : null`). Verificado en levante (3 sitios), producción (3),
   engorde (2); reproductora no abre ninguna.
4. Ruta en `decidir-encolable.funcion.ts` (mapa ruta → tipo, con `$` para no capturar sub-recursos).
5. Toast con `esRespuestaPendiente` en la pantalla que guarda.
6. Tests: xUnit del cálculo puro + spec de la ruta en el front.
7. Smoke HTTP con JWT minteado + `X-Secret-Up` cifrado, y **limpieza por la API** cuando se pueda
   (si da 403, por SQL pero **anulando** el histórico, nunca borrándolo).

---

## Orden sugerido

1. **B1** (seguridad, bloquea la jornada offline)
2. **F4.1 — Gastos de inventario** (nivel 1, valida el patrón en un módulo no-captura)
3. **Emisor de `requiere_cuadre`** (habilita el nivel 2)
4. **F4.2 — Gestión de inventario / inventario de aves** (nivel 2)
5. **Grafo `client_entity_id`** (habilita el nivel 3)
6. **F4.3 — Movimientos, traslados, huevos, ventas** (nivel 3)
