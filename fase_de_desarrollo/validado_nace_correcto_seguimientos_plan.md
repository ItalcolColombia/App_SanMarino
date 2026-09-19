# Plan — `validado` nace bien en TODO seguimiento (y se puede comprobar) · 18-sep-2026

Origen: hallazgo lateral del análisis de «los huevos quedan en 0» (Santa Reyes): los registros de Producción con consumo por
ítems quedaban `validado = false` aunque la doble validación estuviera apagada. Pedido del usuario: «soluciónalo con la mejor
práctica y mejorando el flujo».

## Qué significa `validado` (la definición que manda)

`validado` = **«su efecto ya se aplicó»**, no «alguien apretó el botón» (así lo fijó el arreglo de agosto: `Validado = !separa`).
Un registro está sin validar solo mientras tiene alimento/aves **separados** (reservas ACTIVAS) esperando su aplicación.
Con la doble validación apagada el registro descuenta AL GUARDAR, así que **tiene que nacer validado**.

## Lo medido (copia de producción cargada el 18-sep ~12:20; solo lectura)

| # | Qué | Filas | Detalle |
|---|---|---:|---|
| 1 | Producción, `validado=false`, **sin ninguna reserva**, empresa con flag APAGADO | **4** | #687, #692, #696, #697 (Santa Reyes, 14/09, creados el 18-sep). Los cuatro tienen su `Consumo` aplicado en `inventario_gestion_movimiento` (1232, 1215, 1180 y 966 kg): el efecto YA se aplicó ⇒ tienen que estar validados. **Es el hallazgo.** |
| 2 | Producción, `validado=false` **con reserva ACTIVA**, flag APAGADO | 6 | #676–#681 (13/09, creados el 15-sep con el flag ENCENDIDO): 7.011 kg de alimento y 259 aves **separados y nunca aplicados**. Son pendientes legítimos que quedaron en el limbo porque el flag se apagó con ellos pendientes. **No se tocan.** |
| 3 | Reservas ACTIVAS cuyo registro ya no existe | 1 | alimento 982 kg del #682 (borrado). Además, el borrado con el flag ya apagado tomó el camino «devolver stock»: `inventario_gestion_movimiento` #16859 = *Ingreso 982 kg «devolución por eliminación»* **sin un Consumo previo**, registrado sobre el ítem 373 del catálogo y no sobre el 365 que consumen los registros reales (renglón de stock aparte, silo 6, sin consumidor). |

Solo Santa Reyes tiene filas en 1–3: Sanmarino, Ecuador y Demo (flag apagado) y Panamá (flag encendido, 22 pendientes
legítimos de engorde) están limpias. Levante y engorde no tienen ni una fila del tipo 1.

## Causa del tipo 1 (una rama)

`ProduccionService.CrearSeguimientoAsync`: la rama «Colombia modelo B + consumo por ítems» (`!separa && modelo == B && useItems`)
persiste y hace `return` **antes** de `entity.Validado = !separa`, y la columna nace en su default de BD (`false`). Con el flag
apagado esa rama es justo la de Santa Reyes. Los demás módulos fijan `Validado` en el initializer de la entidad, por eso ellos no.

## Por qué no basta con mover una línea (la clase de defecto)

Es la **cuarta vez** que aparece lo mismo, siempre por el mismo motivo —el default es `false` y cada creador tiene que
acordarse—: los Crud (16-ago), los 4 renglones de traslado de aves, el cruce de reproductora (25-ago) y ahora esta rama.
Quedan **creadores de sistema sin la marca**: `ArrastreHuevosLevanteService`, `MovimientoAvesService` (levante y producción),
`TrasladoHuevosService` (×3), `MigracionService.MovimientosAves` (×2) y `ProduccionDiariaService`. Hoy son latentes (nadie lee
`validado` con el flag apagado); el día que una empresa de postura encienda el flag, sus filas aparecerían pendientes, pasarían
a EN RETRASO a las 24 h y **bloquearían el alta de días nuevos** sin tener nada que validar.

## Enfoque — capas, de la más específica a la más general

1. **Al nacer la entidad, antes de ramificar** (`ProduccionService.Seguimiento.cs`): `entity.Validado = !separa` se fija justo
   después de resolver `separa`, no más abajo. Cubre también la fila del arrastre de huevos que se fusiona con el registro.
2. **Seguro por construcción:** `Validado = true` como valor inicial de la propiedad en las 3 entidades
   (`SeguimientoProduccion`, `SeguimientoDiario` —levante—, `SeguimientoDiarioAvesEngorde`). Un creador que no diga nada nace
   validado —no hay reserva que aplicar—; solo los 4 caminos que separan dicen `false` de forma explícita (`!separa`), como hoy.
   Cierra la clase entera para todo creador de C# presente y futuro, sin tocar ninguno.
3. **Reparación de datos** (migración EF *data-only*, idempotente): marca `validado = true` solo donde se cumplen **las tres**:
   empresa con el flag APAGADO, sin ninguna reserva (de ninguna clase ni estado) y no borrado. No toca `updated_at` ni
   `validado_at`. Con la simulación en transacción se verifica que afecta exactamente #687, #692, #696 y #697.
4. **Comprobación:** `backend/sql/verificar_validado_sin_reserva.sql` (solo lectura; termina en ROLLBACK) mide las tres
   situaciones de arriba y simula la migración dos veces (la 2.ª debe decir `UPDATE 0`). Queda como **paso previo a encender la
   doble validación en cualquier empresa**: los tres conteos tienen que dar 0.
5. **Tests** (xUnit): contrato de las 3 entidades —«sin asignar, nace validada»— para que nadie «limpie» el `= true` sin ver por
   qué está.

## Lo que NO se hace (y por qué)

- **No se cambia el default de la columna en la BD.** EF omite los `false` de un `bool` con `HasDefaultValue(false)` (el valor
  centinela) y deja que ponga el default la BD. Con default `true` en la BD, cada registro que SEPARA nacería validado, y
  durante un deploy rodante las tareas viejas —que siguen omitiendo el `false`— lo harían con Panamá encendida: alimento
  reservado que nunca se aplica. El default de BD queda en `false` y la configuración de EF **no se toca**; el valor inicial de
  la propiedad (`true`) sí viaja explícito en el INSERT.
- **No se unifica el `return` anticipado de Colombia** con el flujo general: es camino de inventario sin test de integración y
  el defecto queda cerrado por las capas 1 y 2 sin reordenar persistencia + consumo.
- **`fn_migracion_seguimiento` (SQL de «Migraciones Masivas») sigue insertando sin `validado`** y nace en el default de BD
  (`false`). Reescribir esa función excede este cambio; queda documentado y el script de verificación lo detecta antes de encender
  el flag en una empresa de postura.
- **No se tocan los datos del tipo 2 y 3** (#676–#681, la reserva del #682 y el +982 kg): aplicarlos o liberarlos es una decisión
  de operación (afecta inventario y aves), no de este arreglo. Se dejan medidos y listados en el script.
- **No se agrega un «guardia» al apagar el flag** (bloquear el apagado mientras haya reservas ACTIVAS): es cambio de
  comportamiento de una operación de administración y la causa raíz de los tipos 2 y 3. Se propone aparte.

## Archivos

- `backend/src/ZooSanMarino.Infrastructure/Services/Funciones/ProduccionService.Seguimiento.cs` (capa 1).
- `backend/src/ZooSanMarino.Domain/Entities/SeguimientoProduccion.cs`, `SeguimientoDiario.cs`, `SeguimientoDiarioAvesEngorde.cs` (capa 2).
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260918210000_NormalizarValidadoSeguimientosSinReserva.cs` + `.Designer.cs` (capa 3; sin tocar el ModelSnapshot).
- `backend/sql/verificar_validado_sin_reserva.sql` (capa 4).
- `backend/tests/ZooSanMarino.Application.Tests/SeguimientoNaceValidadoTests.cs` (capa 5).

## Casos de prueba

- Unitarios: las 3 entidades sin asignar → `Validado == true`; asignando `false` se conserva (el camino que separa).
- Migración sobre la copia real, en transacción con ROLLBACK: afecta **exactamente** {#687, #692, #696, #697}; #676–#681 (con
  reserva) y los registros de Panamá (flag encendido) quedan idénticos; la 2.ª pasada da 0.
- Backend aislado sobre un clon de la copia real (nunca RDS), con la migración aplicada por el arranque de la app:
  1. Flag APAGADO + Colombia + consumo por ítems (Santa Reyes): 201, `validado = true`, `Consumo` aplicado, sin reservas.
  2. Flag APAGADO, registro clásico sin ítems: `validado = true` (sin regresión).
  3. Flag ENCENDIDO en un lote sin pendientes: 201, `validado = false`, reservas ACTIVAS y **sin** movimiento (sigue separando).
  4. El mismo registro con ítems del punto 1 sobre la fila del arrastre de huevos (merge): queda validado.
- `dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`.

## Resultado (18-sep-2026)

- **Build y tests:** `dotnet build` de la solución completa → 0 errores, 0 advertencias. `dotnet test` → Application.Tests
  **4.316/4.316** y Domain.Tests 1/1 (los 5 tests nuevos de `SeguimientoNaceValidadoTests` + los 74 de validación en verde).
  El gate `verificar-sql-llega-por-migracion.js` sigue en OK (`verificar_*` está exento por prefijo).
- **Simulación de la migración sobre la copia real** (`verificar_validado_sin_reserva.sql`, ROLLBACK): [2] lista exactamente
  `{687, 692, 696, 697}`; 1.ª pasada `UPDATE 4 / 0 / 0`; 2.ª pasada `0 / 0 / 0`; filas con reserva que cambiaron = 0; filas de
  empresas con el flag encendido que cambiaron = 0. Resumen: normalizables 4 → 0, limbo 6, reservas huérfanas 1.
- **Smoke con backend aislado sobre un clon de la copia real** (`:5501`, content root propio sin ninguna referencia a RDS,
  `pg_stat_activity` con una sola conexión y al clon; la copia original quedó en 398 migraciones y con sus 10 filas `false`):
  - **El arranque aplicó la migración sola** (399 en el clon): #687, #692, #696 y #697 → `validado = true`; #676–#681 siguen
    `false` con su reserva ACTIVA.
  - Flag APAGADO + Colombia modelo B + consumo por ítems (LPP 26): **201, `validado = true`**, `Consumo` de 10 kg aplicado, sin reservas.
  - Flag APAGADO, registro clásico con consumo escalar: `validado = true`.
  - Flag APAGADO, alta sobre una fila de arrastre de huevos que estaba en `validado = false`: se fusiona (mismo id) y queda
    `true` —este caso solo lo cubre la capa 1; el valor inicial no puede arreglar una fila que ya existe—.
  - Flag ENCENDIDO (LPP 28, tras validar el #681 por la API): **201, `validado = false`**, reserva ACTIVA de 10 kg y de 2 aves y
    **ningún** movimiento. El valor inicial `true` no pisa a quien separa.
  - Flag ENCENDIDO, alta sobre una fila de arrastre nacida validada: pasa a `false` con su reserva ACTIVA.
  - Sin advertencias nuevas de EF en el log del arranque.
- **Procesos y datos:** back :5501 detenido, servidores de MSBuild apagados, clon `smoke_validado` eliminado, credenciales y content root borrados.

## Hallazgos de la medición que NO se arreglan acá (decisión de operación)

Todos salen del mismo hecho: la doble validación estuvo ENCENDIDA en Santa Reyes al menos del 14 al 18-sep (~10:14; hay reservas
creadas hasta esa hora) y quedó APAGADA con pendientes adentro. Lo que mide `verificar_validado_sin_reserva.sql` ([3] y [4]):

1. **6 pendientes en el limbo** (#676–#681, del 13/09; 7.011 kg y 259 aves separados y nunca aplicados). Con el flag apagado no
   hay botón de validar. Si se vuelve a encender, los 6 salen EN RETRASO y **bloquean el alta de días nuevos** de sus lotes (lo
   confirmó el smoke: `pendientes = 1, vencidos = 1, bloqueaAlta = true`) hasta validarlos; validar por la API sí aplica el
   consumo contra el stock real del ítem 365 (#678 y #681 validaron en el clon).
2. **1 reserva huérfana** (alimento 982 kg del #682, ya borrado).
3. **Un Ingreso sin Consumo previo**: al borrar el #682 con el flag ya apagado, el camino «devolver stock» registró
   `Ingreso 982 kg` (movimiento #16859) sobre el ítem 373 del catálogo, no sobre el 365 —el que consumen los registros reales—,
   y creó un renglón de stock aparte (silo 6) sin consumidor. Es el riesgo que el propio código evita cuando el flag está ENCENDIDO
   («devolver stock acá sería INFLAR el inventario con kilos que jamás salieron»).

**Propuesta aparte (no implementada):** un guardia al APAGAR `requiere_validacion_seguimiento_diario` que rechace el cambio mientras
la empresa tenga reservas ACTIVAS y diga cuántas y de qué lotes; y que borrar/editar un registro decida por las reservas del
propio registro y no solo por el flag actual de la empresa.
