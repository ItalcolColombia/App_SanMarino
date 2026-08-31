# Cierre de los 13 casos ya resueltos de Sanmarino, Panamá y Ecuador (31-ago-2026)

Continúa [`cierre_tickets_santa_reyes_italjira_plan.md`](cierre_tickets_santa_reyes_italjira_plan.md),
que cerró los de Santa Reyes. Acá van los **13 casos de las otras tres empresas** cuyo arreglo está
verificado en el código y desplegado en producción, pero que siguen abiertos en el tablero.

## 0. Qué queda abierto y por qué (medido, no asumido)

De los 15 casos no cerrados de otras empresas, **13 tienen el trabajo terminado**:

- **11 en `SOLUCIONADO`** — esperan la confirmación del solicitante, que es la única vía a `CERRADO`
  (`TicketService.ConfirmarCierreAsync` exige `EsSolicitante`). Llevan de **3 a 25 días**. Tres de
  ellos (`TK-000020`, `TK-000164`, `TK-000165`) se marcaron por migración y no tienen ni nota de
  `SOLUCIONADO` ni correo enviado (`notificado_correo = false`): el solicitante **nunca supo** que
  su caso estaba resuelto, así que no lo va a cerrar solo.
- **2 en `EN_ANALISIS`** — resueltos y nadie movió la tarjeta.

Los **2 que NO entran**: `TK-2026-000183` (CAROLINA, `EN_IMPLEMENTACION`) tiene trabajo real
pendiente — el diagnóstico está, los datos no se corrigieron a propósito y el mecanismo de origen
sigue vivo en `InventarioGestionService.StockMutacion.cs:118-145`; y `TK-2026-000001`
(`TRANSFERIDO`, «pruebas Moises» del 30-jun) es un caso de prueba, no una solicitud.

## 1. La verificación, caso por caso (contra el código y `origin/main-produccion`)

| Caso | Empresa | Evidencia del arreglo | En prod |
|---|---|---|---|
| `TK-2026-000012` | Sanmarino | Campo «Fecha del movimiento» con min/max en `modal-movimiento-aves.component.html:101-110` (`00ff4b5`, `8eea14a`) | ✅ |
| `TK-2026-000013` | Sanmarino | `tipo_alimento` varchar(100)→**500**, migración `20260806063157` (`2a35d63`) | ✅ |
| `TK-2026-000014` | Sanmarino | Misma causa; engorde por `20260806074016` (`92e1cb5`) | ✅ |
| `TK-2026-000015` | Ecuador | `7339c61` — un lote sin cerrar absorbía el ciclo siguiente | ✅ |
| `TK-2026-000020` | Sanmarino | No-bug documentado con la verificación en producción en la migración `20260814130000` (168 registros, 24 semanas exactas) | ✅ |
| `TK-2026-000163` | Panamá | Corrección de datos: **0 grupos** de ingresos duplicados hoy en Panamá | ✅ dato |
| `TK-2026-000164` | Panamá | `b355f71` — la doble validación separa y descuenta solo al validar | ✅ |
| `TK-2026-000165` | Panamá | `ValidacionSeguimientoCalculos.Canonico` vivo y **0 referencias** a la tabla inexistente en `ValidacionSeguimiento/` | ✅ |
| `TK-2026-000166` | Panamá | Resuelto por otra vía: `InventarioGestionService.Consulta.cs:276-324` descuenta las reservas activas con el silo en la clave y expone `ReservadoKg` + `DisponibleKg` derivado | ✅ |
| `TK-2026-000176` | Ecuador | `299c816` — las grillas mostraban el SALDO bajo el rótulo «aves encasetadas» | ✅ |
| `TK-2026-000177` | Ecuador | `a9fd721` + `3988183` (el CHECK que lo trababa) | ✅ |
| `TK-2026-000185` | Ecuador | `c13b9ef` — el botón Actualizar apagado al editar | ✅ |
| `TK-2026-000187` | Panamá | `1191b39` — sin día 0 en indicadores, la reproductora hereda la hora del lote. En prod vía PR #89 | ✅ |

## 2. Migración `20260831130000_CerrarTicketsResueltosOtrasEmpresas` (data-only)

Mismo patrón que la de Santa Reyes, con dos diferencias que impone el caso:

- **Se localiza por `codigo`, no por título.** Estos casos los creó la aplicación, no un seed: su
  `codigo` (`TK-2026-NNNNNN`, derivado del id) es el identificador de negocio estable y visible, y
  varios títulos vienen tipeados por el usuario (`ERROR EN LA FEHCA`) — comparar por texto libre es
  frágil. Se cruza además contra el **nombre de la empresa** esperada.
- **Fail-safe por estado, no solo por idempotencia.** Cada caso declara el estado en el que se lo
  espera; si en producción ya está `CERRADO`, o alguien lo reabrió a otro estado, la migración lo
  **saltea con `RAISE NOTICE`** en vez de forzarlo. Cerrar a ciegas un caso que alguien reabrió
  sería peor que no cerrarlo.

Qué escribe:

1. **Los 2 de `EN_ANALISIS`** reciben `solucion_descripcion` + `fecha_solucion` (no la tenían) y
   ambas notas, `SOLUCIONADO` y `CERRADO`.
2. **Los 11 de `SOLUCIONADO`** conservan su solución y su `fecha_solucion` original; se les agrega la
   nota de `CERRADO`. A los **3 sin nota de `SOLUCIONADO`** (`20`, `164`, `165`) se les siembra
   también esa nota, fechada en su `fecha_solucion` real: repara el hueco de línea de tiempo que dejó
   haberlos marcado por SQL.
3. **Los 13** reciben `estado = CERRADO`, `fecha_cierre_solicitante`, `cerrado_por_user_id`,
   `updated_by_user_id`, `updated_at`.

La nota de cierre dice, textual, que **el cierre lo hizo la gestión y no el solicitante**, cuántos
días esperó la confirmación, qué evidencia se verificó, y que el caso se reabre o se registra uno
nuevo si el problema vuelve. Es la diferencia entre un cierre auditable y uno que borra el rastro.

`Down()` devuelve cada caso a su estado previo (`SOLUCIONADO` u `EN_ANALISIS`), limpia solo lo que
escribió el `Up` (comparando contra sus propios valores) y borra las notas sembradas.

## 3. Casos de prueba

1. `Up()` dos veces en una transacción revertida ⇒ la 2ª pasada no mueve ninguna fila ni duplica notas.
2. `Down()` tras `Up()` ⇒ 11 en `SOLUCIONADO` con su `fecha_solucion` original intacta, 2 en
   `EN_ANALISIS` sin solución, y las notas sembradas borradas.
3. Fail-safe: forzar un caso a `CERRADO` antes del `Up()` ⇒ lo saltea con `NOTICE`, sin tocarlo.
4. `TK-2026-000183` y `TK-2026-000001` **no se tocan**, ni ningún caso de Santa Reyes.
5. `dotnet build` + `dotnet test` verdes.
