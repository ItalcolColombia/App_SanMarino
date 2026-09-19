# Plan — Al apagar la doble validación se validan los pendientes; el limbo de Santa Reyes se resuelve por migración · 19-sep-2026

Origen: respuesta del usuario a los hallazgos de `validado_nace_correcto_seguimientos_plan.md` (commit `fa301b5`):

1. **Guardia al apagar el flag** → «sería más fácil que si se le quita a la empresa el validador, todos sus registros pendientes
   pasan a validarse y se desactiva; solo si se desactiva luego de estar activado».
2. **El limbo de Santa Reyes** → «valida los registros **por migración** para que tome los cambios, y que todo esté por migración
   para que se aplique en producción».

## Decisiones (del usuario) y cómo se leen

| Decisión | Diseño |
|---|---|
| Apagar `requiere_validacion_seguimiento_diario` valida primero todos los pendientes de la empresa | El apagado **ON → OFF** ejecuta antes la validación de cada pendiente (mismo camino que el botón ✓: aplica alimento y aves, marca las reservas APLICADAS). El flag se apaga **solo si no queda ninguno**. |
| «Solo si se desactiva luego de estar activado» | Solo la transición `true → false`. Flag ya apagado, o encendido, o un `PUT` que no toca el flag: comportamiento de siempre. |
| El limbo de Santa Reyes por migración | Migración EF (SQL) que **replica `ValidarAsync`** para los pendientes con reserva de una empresa con el flag APAGADO; más la reserva huérfana y el Ingreso fantasma. Verificada por prueba **diferencial** contra la API. |
| «Borrar/editar decide por sus reservas» | **No se implementa.** Con el apagado validando primero, el estado «flag apagado con pendientes» ya no se puede producir; sin ese estado los caminos de borrado/edición bajo flag apagado son correctos por construcción. Queda anotado como defensa en profundidad opcional. |

## Parte A — El apagado valida los pendientes (código)

### Comportamiento

En `CompanyService.UpdateAsync`, si la empresa tiene el flag `true` y el DTO manda `false`:

1. `IValidacionSeguimientoService.ValidarPendientesDeLaEmpresaAsync(companyId)` recorre los lotes con pendientes (levante, producción,
   engorde, reproductora) y valida cada uno con la lógica de **validar en bloque** (una transacción por registro, orden cronológico,
   corte en el primer fallo *dentro del lote*, tope de 60 por pasada → se repite hasta agotar).
2. Si **algo falló o no se intentó**: se lanza `InvalidOperationException` **antes de tocar nada** (ni el flag ni el resto de los campos
   del DTO se guardan) → el controller responde **400** con el detalle (registro, lote, fecha, motivo: «Stock insuficiente…»). El flag
   sigue encendido; lo ya validado queda validado (es un estado normal con el flag ON) y reintentar retoma donde paró (idempotente).
3. Si todo se validó: el flag se apaga y se guarda como siempre.

Por qué **una transacción por registro y no una para todo**: es como ya funciona «validar en bloque» (el éxito parcial es el punto de
esa función), evita una transacción enorme sobre inventario de varias granjas y el invariante que importa —**flag apagado ⇒ ningún
pendiente**— se sostiene igual porque el flag solo se apaga cuando no queda nada.

### Reglas que se respetan

- **Empresa activa = empresa que se apaga**, pero **solo si hay algo que validar**: el inventario, las aves y el cruce se aplican bajo la
  empresa activa (`ValidarAsync` ya lo exige: `EsDeLaEmpresaActiva`; `RegistrarConsumoAsync` de Ecuador/Panamá rechaza una granja ajena).
  Sin pendientes, apagar el flag no exige nada. Con pendientes y otra empresa activa: 400 «cambie a esa empresa y repita».
- **Sin exigir el permiso `*.validar` a quien administra empresas.** La operación la autoriza la policy `AdminEmpresas` del `PUT`; exigir
  además el permiso por módulo podía bloquear justo al administrador que pide el apagado, y un `UnauthorizedAccessException` sale como
  **401** (manejador global), que el interceptor del front trata como error de autenticación y puede cerrarle la sesión
  (`debeCerrarSesionPor401`). Queda auditado en `validado_por` (el usuario que apagó).
- **Reproductora** entra: validar es confirmar (`confirmado = true`) y eso dispara el cruce a engorde, igual que con el botón.
- El apagado **no** valida nada si el DTO no toca el flag o ya estaba apagado.

### Piezas

- `Application/Calculos/ApagadoDobleValidacionCalculos.cs` (puro, con tests xUnit): `EsApagado(actual, solicitado)`,
  `PuedeValidarDesdeEmpresaActiva(objetivo, activa)`, mensajes (empresa activa distinta; fallos con tope de líneas).
- `ValidacionSeguimientoService`: se extrae de `ValidarAsync` el núcleo sin chequeo de permiso (`ValidarRegistroAsync`) y de
  `ValidarPendientesDelLoteAsync` su núcleo (`ValidarPendientesDelLoteSinPermisoAsync`); comportamiento idéntico para los endpoints
  existentes. Nuevo `Funciones/ValidacionSeguimientoService.ValidarEmpresa.cs` con la enumeración de lotes y el bucle.
- `CompanyService.UpdateAsync` (+ `IValidacionSeguimientoService` por DI) y `CompanyController.Update` (`InvalidOperationException` → 400).
- Front `company-management`: confirmación al **desmarcar** el flag en una empresa existente («se validarán ahora todos los pendientes…»)
  y descripción del flag actualizada. Función pura + spec.

## Parte B — El limbo de Santa Reyes por migración

### Lo que hay que corregir (medido en la copia de producción del 18-sep)

| # | Estado | Corrección |
|---|---|---|
| 1 | 6 pendientes con reserva ACTIVA (#676–#681, 7.011 kg, 259 aves), empresa con flag APAGADO | **Validarlos**: aplicar el consumo (Colombia modelo B: el ítem 373 del catálogo → ítem 365 «PREPICO 100 STA RITA JAULA AC» por código, por silo), reservas APLICADAS, registro `validado = true`. En producción no se mueven aves (el saldo lo manda la fn; ver `AplicarAvesAsync`). |
| 2 | 1 reserva ACTIVA sin registro dueño (#682, 982 kg) | **Liberarla** (`LIBERADA`, `liberada_at`): es lo que hace `LiberarAsync` cuando se borra un registro con el flag encendido. |
| 3 | `Ingreso` de 982 kg del borrado del #682 (movimiento #16859), sin Consumo previo, sobre el **ítem 373 real** («MQ PREPICO ARRANQUE SR INTELLA», código 2787) en el silo 6: un renglón de stock de un alimento que no se tiene | **Compensarlo** con un `AjusteStock` («Ajuste manual», el mismo que escribe «Ajustar stock» de la pantalla) y bajar ese renglón exactamente esos 982 kg. El movimiento original se conserva (auditoría). |

La causa de #3 es una **colisión de ids**: el camino de devolución por borrado tomó el `catalogItemId` 373 como si fuera un
`item_inventario.id` (que existe, y es OTRO ítem). Es el mismo riesgo que ya documenta `ColombiaInventarioConsumoService` («ya no se
adivina la tabla de origen por existencia en catalogo_items»). Con el apagado validando primero deja de poder producirse.

### Diseño de la migración de validación (SQL, `DO $$`)

**Por qué SQL y no el servicio:** lo único que corre en producción son las migraciones EF (`Database__RunMigrations=true`); no hay forma
de invocar servicios C# desde una migración, y este repo ya corrige datos así (`Reimputar…`, `Backfill…`). Por eso la lógica de
`ValidarAsync` para este caso se replica **fielmente** y se **prueba contra la API** (ver «Verificación»).

Criterio de selección (todas a la vez): registro de producción `validado = false`, no borrado, de una empresa con
`requiere_validacion_seguimiento_diario = false`, con al menos una reserva ACTIVA. Alcance: **Colombia (`pais_id = 1`), modelo B a nivel
granja** —el único caso que existe—; un registro con reservas de otro país se omite con `NOTICE` (necesita otro camino de inventario).

Por registro, en orden cronológico y dentro de un sub-bloque con `EXCEPTION` (savepoint: un fallo deshace **solo ese** registro y la
migración sigue; **nunca aborta el arranque**):

1. Reservas de alimento agrupadas por (ítem, tipo de ítem, silo) como hace `AItemConsumo`; por cada clave:
   - modo de la empresa dueña de la granja (`maneja_inventario_por_silo`): por silo ⇒ el silo es obligatorio y debe ser un silo activo de
     la granja; clásico ⇒ no debe traer silo (`ConsumoSiloCalculos.ValidarClaves`);
   - ítem del inventario: catálogo → por **código** (`lower(trim)`) a `item_inventario` de la empresa y país 1; directo → el id si
     pertenece a la empresa (`ColombiaInventarioIdResolutionCalculos`);
   - stock a nivel granja `(farm, ítem, nucleo NULL, galpon NULL, silo)` con `UPDATE … SET quantity = quantity − q, updated_at = now()
     WHERE id = … AND quantity >= q` (el descuento atómico de `DescontarStockAtomicoAsync`; sin saldo ⇒ el registro se omite);
   - movimiento `Consumo` / estado `Consumo`: empresa y país **de la granja**, unidad del ítem (o `kg`), referencia
     `Seguimiento producción #<id> <yyyy-MM-dd> (validado)` (`ReservaSeguimientoCalculos.ReferenciaInventario`), `created_at` = el **día del
     seguimiento a las 18:00 UTC** (`AnclaConsumoUtc`), sin usuario. El trigger `trg_inventario_gestion_movimiento_lote_hist` espeja el
     movimiento al histórico unificado igual que con el servicio.
2. Reservas de alimento y de aves ACTIVA → APLICADA con `aplicada_at = now()`.
3. Registro: `validado = true`, `validado_at = now()`, `validado_por = 'migracion'`, `updated_at = now()`, con `WHERE validado = false`.

**Idempotente:** un registro validado sale del criterio ⇒ re-ejecutar no hace nada. **Robusta al stock del despliegue:** el stock de hoy no
es el de la copia; sin saldo el registro queda pendiente y `verificar_validado_sin_reserva.sql` lo lista.

### Migración de limpieza (reserva huérfana + Ingreso fantasma)

- **Reservas huérfanas** (genérico): toda reserva ACTIVA (alimento o aves) cuyo registro dueño ya no existe pasa a LIBERADA
  (`PRODUCCION`, `LEVANTE`, `ENGORDE`, `REPRODUCTORA`; producción cuenta como inexistente si está borrado).
- **Ingreso fantasma** (específico y con guardas): el movimiento `Ingreso` con referencia exacta
  `Seguimiento producción #682 (devolución por eliminación)` (a nivel granja, sin núcleo ni galpón) se compensa con un `AjusteStock`
  (referencia `Correccion del movimiento #<id>`, que es también su marca de idempotencia) **solo si** (a) el registro #682 ya no existe,
  (b) no hay ningún `Consumo` de `Seguimiento producción #682` y (c) el renglón de stock de ese ítem y silo conserva **al menos** esos
  kilos (nadie lo usó). Sin cumplirlas no hace nada y lo dice con `NOTICE`. El movimiento original se conserva (auditoría).

## Verificación

1. **Prueba diferencial (la que decide):** dos clones de la copia real. En A se validan los 6 registros por la **API** (`POST
   /api/SeguimientoValidacion/PRODUCCION/{id}/validar`); en B corre la **migración**. Se comparan, normalizando ids/usuarios/relojes:
   `inventario_gestion_movimiento` (filas nuevas), `inventario_gestion_stock` (tabla entera), `seguimiento_reserva_*` (estados),
   `seguimiento_diario_produccion` (validado) y `lote_registro_historico_unificado` (filas nuevas). Tienen que coincidir.
2. Arranque real de la app sobre un clon con `RunMigrations=true`: las dos migraciones se aplican solas; `verificar_validado_sin_reserva.sql`
   da limbo 0, huérfanas 0, normalizables 0; segunda corrida = nada.
3. Casos borde de la migración: sin stock (omite y sigue), silo no habilitado, ítem sin equivalente, empresa con flag ENCENDIDO (no toca),
   registro ya validado (no toca).
4. Parte A en el clon con el backend aislado: apagar con pendientes → valida y apaga; apagar con un pendiente sin stock → 400 con detalle, flag
   sigue ON, nada del DTO se guarda; apagar otra empresa con pendientes y empresa activa distinta → 400; apagar sin pendientes → apaga.
5. `dotnet build` (0 errores/advertencias) + `dotnet test` (tests nuevos del cálculo puro) + `yarn build` y `ng test` del front tocado.

## Lo que NO se hace

- No se cambia el default de `validado` en la BD (ver el plan anterior).
- No se agrega la decisión por reservas propias en borrar/editar (ver la tabla de decisiones).
- No se tocan otras empresas: los `Ingreso de devolución por eliminación sin Consumo previo` que aparecen en Ecuador (22 filas) son
  históricos de otra época y otro flujo; requieren su propia auditoría.
- Reencender la doble validación en Santa Reyes sigue siendo decisión de operación: después de esta migración no queda ningún pendiente,
  así que reencenderla ya no trae el bloqueo por vencidos.

## Archivos

Backend: `ApagadoDobleValidacionCalculos.cs`, `IValidacionSeguimientoService.cs`, `ValidacionSeguimientoService.Validar.cs`,
`…ValidarEnBloque.cs`, `…ValidarEmpresa.cs` (nuevo), `CompanyService.Crud.cs`, `CompanyController.cs`, 2 migraciones (+ Designer),
`verificar_validado_sin_reserva.sql` (ajuste), tests. Front: `flags-empresa.funcion.ts` (+ spec), `company-management.component.ts`.

## Resultado (19-sep-2026)

- **Build y tests:** `dotnet build` de la solución → **0 errores, 0 advertencias**. `dotnet test` → Application.Tests **4.340/4.340** (24 nuevos, los de
  `ApagadoDobleValidacionCalculosTests`) y Domain.Tests 1/1. El gate `verificar-sql-llega-por-migracion.js` sigue en OK (el `.sql` nuevo es `verificar_*`).
- **Parte B — prueba diferencial (la que decide):** dos clones de la copia real. En uno, los 6 registros (#676–#681) se validaron por la **API**
  (`POST /api/SeguimientoValidacion/PRODUCCION/{id}/validar`, con los binarios de ANTES de este cambio); en el otro corrió el **SQL extraído tal cual de la
  migración**. Movimientos nuevos, `inventario_gestion_stock` entera, reservas de alimento y aves, registros de producción, histórico espejado y el conteo de
  filas de TODAS las tablas: **idénticos** (la única diferencia es la fila de `sesiones_activas` del propio smoke).
- **Parte B — bordes** (otro clon, con fixtures): sin stock en el silo → ese registro se omite y sigue con los demás («2 validados, 3 omitidos»); silo inactivo;
  ítem sin equivalente; cada omitido queda con `validado = false`, la reserva ACTIVA y ningún movimiento (el sub-bloque deshace el «marcar primero»); un registro
  ya validado no se toca; 2.ª corrida = 0; empresa con el flag ENCENDIDO = 0. Limpieza: libera la reserva huérfana, compensa los 982 kg con UN `AjusteStock` (2.ª
  corrida = 0) y, si el renglón ya no tiene esos kilos (probado con 500), **no** compensa y lo avisa.
- **Parte B — arranque real** (clon con `RunMigrations=true`): la app aplicó sola las tres migraciones pendientes (`20260918210000`, `20260919120000`,
  `20260919121000`; historial 398 → 401); `verificar_validado_sin_reserva.sql` pasó de **4 / 6 / 1 / 1** a **0 / 0 / 0 / 0**; 6 consumos por 7.011 kg, un ajuste, el renglón
  fantasma en 0 y 6 registros con `validado_por = 'migracion'`.
- **Parte A — backend aislado sobre un clon de la copia real** (Santa Reyes con el flag ENCENDIDO y sus 10 pendientes: los 6 con reserva más los 4 sin reserva):
  1. Empresa activa distinta con pendientes → **400** «Para apagar … hay que validar 10 registros pendientes … Cambie a esa empresa … No se cambió nada»; flag, teléfono,
     pendientes y movimientos intactos. (Con un token sin la marca de super admin el `PUT` sale 403 por la policy `AdminEmpresas`, antes de llegar al servicio.)
  2. Un lote sin stock (silo 5: 1.000 kg contra 1.200) → **400** con el detalle («Producción, lote 27: #680 (13/09/2026): Stock insuficiente … disponible 1000 kg, requerido
     1200 kg») y «2 de 10 … no se pudo validar»; el flag sigue encendido, **el teléfono del mismo `PUT` no se guardó**, y quedaron validados los otros 8 (lo ya hecho se conserva).
  3. Se corrige el stock y se reintenta → **200**: flag apagado, teléfono guardado, 0 pendientes, 6 consumos por **7.011 kg**, `validado_por` = el usuario que apagó.
     Las reservas ACTIVAS que quedan son las huérfanas (las limpia la migración B2; el apagado no las toca).
  4. `PUT` que ENCIENDE el flag (falso → verdadero): no valida nada. `PUT` que no manda el flag con el flag encendido: se conserva.
  5. Apagar **sin pendientes** desde otra empresa activa: **200**, no exige nada.
  Log del API sin errores; una sola advertencia, la esperada del corte del lote 27.
- **Parte A — los otros módulos (Panamá, flag ENCENDIDO):** clon con 21 pendientes de engorde, 4 de reproductora y 24 + 21 reservas ACTIVAS de alimento y de aves.
  `PUT` con el flag en falso y la empresa activa = Panamá → **200**: 0 pendientes de engorde y de reproductora, 0 reservas ACTIVAS, 24 consumos por **41.324,2 kg** con la
  referencia `Seguimiento aves engorde #N (validado)` (el camino de inventario con núcleo/galpón, que exige la empresa activa) y las 4 confirmaciones de reproductora
  dispararon el cruce (filas `SYSTEM_CRUCE`), igual que con el botón ✓. Log del API sin errores.
- **Front:** `ng test` de los dos specs de `company-management` → **16/16**; `yarn build` → **0 errores, 0 advertencias** (la única línea con «warning» es la de yarn, `No license field`).

**Pendiente del usuario:** OK para desplegar. Las migraciones corren solas al arrancar; después conviene correr `verificar_validado_sin_reserva.sql` contra
producción (los cuatro conteos en 0). Un registro que no se pueda aplicar ese día por falta de stock **no aborta el despliegue**: queda pendiente y el script lo lista.
