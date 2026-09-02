# Plan — Acelerar consultas, bajar carga del backend y quitar código sin uso

**Fecha:** 2026-09-02 · **Estado:** auditoría cerrada, ejecución pendiente de decisión de alcance.

Todo lo de abajo está **medido** contra el repo y la BD local (`sanmarinoapplocal`, PG 17.5, puerto
5433), no estimado. Donde algo es una sospecha y no una medición, lo dice.

---

## Enfoque arquitectónico

Tres frentes independientes; se pueden ejecutar por separado y cada uno se valida solo:

| Frente | Qué ataca | Riesgo |
|---|---|---|
| **F1 — Transporte y pipeline** | Bytes en la red y costo por request. No toca ni una consulta. | Muy bajo |
| **F2 — Consultas** | Trabajo que hoy hace C# en memoria y debería hacer la BD. | Medio: es donde se puede cambiar un número |
| **F3 — Código y objetos sin uso** | Superficie muerta (endpoints, tablas de respaldo). | Alto si se borra en bloque |

Regla rectora, de CLAUDE.md: **refactor ≠ cambio de comportamiento.** Toda consulta que se mueva a SQL
tiene que devolver **exactamente** el mismo número, redondeos incluidos. La forma de probarlo es
comparación fila a fila antes/después, no «compila y anda».

---

## Hallazgos medidos

### F1 — Transporte y pipeline

1. **El API no comprime nada.** No hay `AddResponseCompression` en `Program.cs`, no hay nginx delante
   (el único `Dockerfile` publica el binario directo) y el ALB no comprime por su cuenta. Todo el JSON
   —incluidos los reportes, que son los payloads gordos— viaja crudo desde ECS. Los únicos `gzip` del
   repo están en un `.md` de documentación y en artefactos de `bin/`.

2. **`AddDbContext` en vez de `AddDbContextPool`** (`Program.cs:148`). Cada request construye y
   descarta el contexto entero. Tampoco hay `EnableRetryOnFailure`, así que un corte transitorio
   contra RDS es un 500 en vez de un reintento.

3. **El front no cachea casi nada:** `shareReplay` aparece en **3** archivos de **120** que inyectan
   `HttpClient`. Cada suscripción nueva = request nuevo. Además hay polling fijo cada 2–5 s en 6
   pantallas (`db-studio` 4 s, `mapas` 2 s ×2, gestión de usuarios 3/4/5 s).

### F2 — Consultas

4. **119 sitios en 65 archivos materializan con `ToListAsync()` y después agregan o filtran en
   memoria.** Verificados a mano los dos peores:
   - `DisponibilidadLoteService.cs:79` — baja **todos** los seguimientos del lote y **todos** los
     traslados para hacer **22 `Sum()`** en C#. Son dos `SELECT *` que deberían ser dos
     `SELECT sum(...), sum(...)`.
   - `MovimientoAves/Funciones/MovimientoAvesService.LoteInfo.cs:76` — 10 casos del mismo patrón en un
     solo archivo.

   ⚠️ De los 119, **una parte es falso positivo** (un `.Any()` sobre una lista corta de ids ya traída
   a propósito, para no repetir el viaje). El plan es revisarlos uno por uno, no convertirlos en masa.

5. **52 filtros no sargables** dentro de `Where(...)`: 19 con `.Date ==`, 17 con `.ToString()`, 16 con
   `.ToLower()`/`.ToUpper()`. Postgres no puede usar el índice sobre una columna envuelta en una
   función: se come la tabla entera. El caso más caro es `.FechaTraslado.Date == fecha.Date`, en el
   reporte técnico de producción.

6. **14 claves foráneas sin índice** en las tablas más grandes (medido en la BD local):

   | Tabla | Columnas FK sin índice | Filas (local) |
   |---|---|---|
   | `inventario_gestion_movimiento` | `item_inventario_id`, `silo_id`, `from_silo_id` | 13.609 |
   | `movimiento_pollo_engorde` | `granja_origen_id`, `granja_destino_id`, `lote_ave_engorde_destino_id`, `lote_reproductora_ave_engorde_destino_id` | 2.114 |
   | `inventario_gasto_detalle` | `item_inventario_id`, `silo_id` | 795 |
   | `inventario_gasto` | `farm_id`, `pais_id`, `lote_ave_engorde_id` | 638 |
   | `inventario_gestion_stock` | `item_inventario_id`, `silo_id` | 578 |

   En local `inventario_gasto` lleva **8.164 seq scans contra 45 index scans** e
   `inventario_gasto_detalle` **11.654 contra 37**. En local eso no duele porque la tabla entra en una
   página; **en producción, con el volumen real, sí.** Hay que confirmar el conteo contra prod antes
   de dimensionar la ganancia.

7. **14 bloques con una llamada a BD dentro de un bucle** (corregido: la primera pasada dijo 17
   porque el detector no manejaba el `foreach` de una sola sentencia **sin llaves** y seguía contando
   hasta el bloque de arriba). 8 de los 14 hacen `SaveChangesAsync()` por iteración.

   Los dos que la primera pasada dio por "casos claros" **no lo eran**, y quedan sin tocar:
   - `AuthService.cs:654` → **falso positivo** del detector. El `foreach` solo asigna
     `IsUsed = true`; el `SaveChanges` ya está afuera.
   - `EmailQueueProcessorService.cs:87` → **deliberado**. Guarda `"processing"` *antes* de intentar
     el envío; si el proceso muere a mitad, el correo no queda como pendiente y no se manda dos
     veces. Batchearlo rompe esa garantía.

   Los que sí están en camino de request y valen la pena: `ColombiaInventarioConsumoService:204`,
   `FarmInventoryReportService:73`, `InventarioGestionService.Traslado.cs:537` y las 3 vueltas de
   `UserFarmScopeService:143-151`. Los de `Migracion/` y `PuentePanama/` commitean por fila para
   aislar la que falla en una carga masiva: **no se tocan**.

8. **17 endpoints GET devuelven la colección completa sin paginar**, en 10 controllers
   (`LotePosturaBase`, `LotePosturaLevante`, `LotePosturaProduccion`, `LoteReproductora`,
   `LoteReproductoraAveEngorde`, `LoteSeguimiento`, `FarmSilo`, `GalponSilo`, `SiloCatalogo`,
   `Mapas`). Hoy aguantan porque las tablas son chicas; es deuda que escala mal.

### F3 — Código y objetos sin uso

9. **1 controller sin ningún lector en el repo:** `ServiceTokensController` (2 endpoints). Ojo: es
   justo el tipo de endpoint que consume un cliente externo (Power BI, un script). **No se borra sin
   confirmar** que nadie de afuera lo llama.

10. **11 endpoints sin ninguna referencia** en el front, la app móvil o el backend (corregido: la
    primera pasada dijo 124 porque unía segmentos estáticos **no adyacentes** — para
    `api/db-studio/tables/{schema}/{table}/columns` buscaba `tables/columns`, que obviamente no
    aparece. El criterio correcto es: huérfano solo si **ningún** segmento estático suyo aparece en
    algún cliente).

    Verificados uno por uno:

    | Endpoint | Veredicto |
    |---|---|
    | `POST api/service-tokens` · `DELETE api/service-tokens/{id}` | **No es código muerto.** Por diseño no tiene cliente en el repo: emite PAT para crones headless que llaman `/api/tickets`, y se opera a mano. **Conservar.** |
    | `POST api/Auth/change-password` | **Duplicado superado.** El front usa `PATCH /api/users/{id}/password` (`UsersController.cs:125`, vía `UserProfileService.changeMyPassword`). Segunda superficie de cambio de contraseña que nadie ejercita ni testea. |
    | `POST api/Auth/change-email` | Backend completo que **nunca se conectó a la UI**: no hay equivalente en `UsersController` ni pantalla en el front. |
    | `GET api/Auth/ping-simple` | Diagnóstico. |
    | `POST api/SeguimientoAvesEngorde/backfill-metadata` | Operativo de una sola vez (backfill de `metadata` jsonb). |
    | `GET api/ExcelImport/template-info` · `GET api/Farm/by-zona-usuario` · `GET api/Galpon/{id}/detail-simple` · `GET api/HistorialInventario/resumen-cambios` · `GET api/HistorialInventario/movimientos-grandes` | Sin lector. Features que quedaron sin UI. |

11. **16 tablas de respaldo vivas en la BD** (`_backup_*`, `_migracion_*`), ~3,4 MB en local. Son
    respaldos de operativos ya cerrados (saldo de alimento, cruce de engorde, remisión duplicada).
    Ocupan espacio, aparecen en cada listado de tablas y confunden al que audita. Borrarlas es
    **irreversible** y necesita OK explícito por tabla.

---

## Orden propuesto (de más ganancia y menos riesgo, a menos)

| # | Cambio | Ganancia | Riesgo | Valida con |
|---|---|---|---|---|
| 1 | Compresión de respuesta (brotli + gzip) | Alta y global | Casi nulo | `content-length` antes/después en 3 endpoints |
| 2 | `AddDbContextPool` + `EnableRetryOnFailure` | Media, global | Bajo | `dotnet build` + `dotnet test` + smoke |
| 3 | Índices de las 14 FK, migración idempotente | Alta en prod | Bajo (`CREATE INDEX IF NOT EXISTS`) | `EXPLAIN` antes/después |
| 4 | `SaveChanges` fuera del bucle en los 2 casos claros | Media | Bajo | Test del módulo |
| 5 | Agregación en SQL en los sitios verificados | Alta por endpoint | **Medio** | **Comparación fila a fila del número** |
| 6 | Filtros sargables (`.Date`, `.ToString`, `.ToLower`) | Alta por endpoint | **Medio** (mueve el borde de la fecha) | Igual que 5 |
| 7 | Verificar y quitar endpoints sin lector | Baja (mantenimiento) | **Alto** | Uno por uno, con OK del usuario |
| 8 | Borrar tablas `_backup_*` | Baja | **Alto, irreversible** | OK por tabla |

## Casos de prueba

- **Compresión:** mismo endpoint con y sin `Accept-Encoding: br` → el cuerpo descomprimido tiene que
  ser **byte a byte idéntico**.
- **Cada consulta movida a SQL:** se congela la salida actual del endpoint, se aplica el cambio, se
  compara **fila a fila**. Cero diferencias o no se mergea.
- **Gate multipaís (CLAUDE.md):** si el cambio toca cálculo compartido de engorde, corre
  `backend/sql/verificar_paridad_saldo_engorde.sql` antes y después; toda empresa que no sea el
  objetivo tiene que salir en 0.
- **Cuadre:** `backend/sql/verificar_cuadre_alimento_engorde.sql` antes y después; si se mueve de la
  línea base, es regresión.
- `dotnet build` sin errores ni advertencias nuevas + `dotnet test` verde + `yarn build` en el front.

## Lo que este plan NO propone

- **Mover el cálculo de negocio a funciones SQL nuevas.** El pedido inicial fue «que sea más funciones
  y proceso». Contra el estado real —45 `fn_*` y 5 `vw_*` ya vivas— la deuda no es falta de funciones:
  es que el C# baja tablas enteras para sumarlas. Se arregla con agregación en la consulta que ya
  existe, no creando otra `fn_`. Y CLAUDE.md es explícito: si un número se calcula en SQL y en C#,
  uno es el dueño y el otro es el test — duplicar fórmulas es exactamente lo que ya rompió el saldo
  de alimento tres veces.

---

## Hallazgo aparte, encontrado al validar: un endpoint que devuelve ceros

Al comprobar la paridad del refactor de `DisponibilidadLoteService` apareció esto, que **no lo causó
el refactor** y por eso no se tocó (refactor ≠ cambio de comportamiento):

`ObtenerDisponibilidadHuevosAsync` filtra `TipoSeguimiento == "produccion"` sobre la entidad
`SeguimientoDiario`, que por `ToTable` mapea a **`seguimiento_diario_levante`**
(`SeguimientoDiarioConfiguration.cs:13`). Medido en la copia local: esa tabla tiene **1.112 filas y
todas son `tipo_seguimiento = 'levante'`** — el filtro no puede coincidir nunca. Mientras tanto,
`seguimiento_diario_produccion` tiene **605 filas, 4 lotes y 3.633.088 huevos**.

O sea: `GET /api/Traslados/disponibilidad/{loteId}` y `/disponibilidad-lpp/{id}` informan **0 huevos
disponibles de todos los tipos, siempre**.

**Alcance acotado:** en la creación de traslado (`TrasladosController.cs:113`) el resultado se usa
solo para leer `disponibilidad.GranjaId` y para un chequeo de nulo — el bloqueo de aves va por
`ValidarDisponibilidadAvesAsync`, que es otro camino. Así que esto **no está bloqueando traslados**;
lo que hace es mostrar la disponibilidad de huevos en cero.

**Por qué no se arregló acá:** apuntar a la tabla correcta cambia el comportamiento visible y entra
en la trampa ya documentada de `loteId` vs `lotePosturaProduccionId` (un dígito de distancia, no
truena, no encuentra nada). Es una entrega propia, con su decisión.
