# Aves disponibles menos lo separado sin validar

**Fecha:** 15ago26 · Cierra el hallazgo que V5 dejó abierto: `ReservaSeguimientoCalculos.DisponibleAves`
no tenía ningún llamador.

---

## 1. Auditoría: dónde el número YA está bien y dónde no

El enunciado «un traslado o una venta pueden despachar aves que un seguimiento sin validar ya dio de
baja» resultó **cierto solo en una parte de las superficies**. Auditado contra el código y, donde se
pudo, contra el backend corriendo:

| Superficie | ¿Resta las bajas sin validar? | Por qué |
|---|---|---|
| Engorde · disponibilidad para venta/despacho (`MovimientoPolloEngordeService.ResumenDisponibilidad`) | ✅ **Sí** | `AvesDisponiblesEngordeCalculos` resta `registradas − aplicadas`. Un registro sin validar está **registrado** (la consulta de bajas no filtra por `validado`) y **no aplicado** (sin fila `BAJA_SEGUIMIENTO`), así que cae entero en «pendientes de aplicar» |
| Reproductora engorde (`LoteReproductoraAveEngordeService`) | ✅ **Sí** | usa el mismo cálculo |
| Levante **con** lote base (`LoteService.GetMortalidadResumenAsync`) | ✅ **Sí** | el saldo se calcula `base − mortCaja − mort − sel − err + trasIn − trasOut` sumando las filas de `seguimiento_diario`; no sale del maestro |
| Levante **sin** lote base (`lpl.AvesHActual`) | ❌ **No** | lee el maestro, que con el flag ON no se descontó |
| Producción (`lpp.AvesHActual`) | ❌ **No** | ídem |

**Verificación empírica del caso cubierto** (lote 168, ItalcolPanama, flag ON): con el maestro en
8.523 hembras, al guardar un seguimiento con 7 bajas **sin validar** el endpoint de aves disponibles
devolvió **8.516**; al borrarlo, **8.523**. O sea: ya se restaban.

**Conclusión:** el trabajo real es `TrasladoAvesDesdeSegService.GetDisponibilidadAsync`, en las dos
ramas que leen el maestro.

---

## 2. El riesgo principal es restar DOS veces

La rama de levante con lote base ya trae las bajas sin validar dentro del saldo. Si se le restara
además la reserva, las bajas se contarían dos veces y el traslado bloquearía aves que sí existen —el
mismo tipo de error que ya ocurrió en este repo (`AvesDisponiblesEngordeCalculos` nació justo de un
doble descuento).

Por eso la reserva se resta **solo** donde el saldo sale del maestro:

- levante ⇒ únicamente en el *fallback* `AvesHActual` (sin lote base);
- producción ⇒ siempre.

Queda fijado con tests, no con un comentario.

---

## 3. Alcance real hoy

La única empresa con `requiere_validacion_seguimiento_diario` es **ItalcolPanama**, que tiene **0
lotes de postura** (65 de engorde). O sea: el hueco es **latente**, no está afectando datos. Se
arregla ahora justamente por eso — con el flag encendido en una empresa de postura (Sanmarino tiene
10 levante y 2 producción, Demo 5 y 2) el traslado empezaría a ofrecer aves ya dadas de baja.

Con el flag OFF no hay reservas activas ⇒ `reservado = 0` ⇒ el número no se mueve.

---

## 4. Archivos

| Archivo | Cambio |
|---|---|
| `Infrastructure/Services/TrasladoAvesDesdeSegService.cs` | inyectar `IValidacionSeguimientoService?` (opcional, sin ciclo de DI: nadie de su cadena depende de este service) y restar la reserva en las dos ramas que leen el maestro |
| `Application/Calculos/ReservaSeguimientoCalculos.cs` | doc de `DisponibleAves`: dónde SÍ y dónde NO se aplica |
| `tests/…/ReservaSeguimientoCalculosTests.cs` | casos de `DisponibleAves` + el de la doble resta |

**Sin cambios de BD ni de front**: el front ya consume `AvesHActual`/`AvesMActual` del DTO, que es lo
que se corrige.

---

## 5. Reglas de negocio

- Reserva **activa** solamente: la `APLICADA` ya salió del maestro y la `LIBERADA` no compromete nada.
- Levante usa el módulo `LEVANTE` y el `lote_postura_levante_id`; producción, `PRODUCCION` y el
  `lote_postura_produccion_id` — que es la clave con la que la separación guardó `LoteRefInt`.
- En postura la reserva nunca es mixta (`LineasDeAves` solo manda a `Mixtas` en lotes de engorde
  mixtos), pero se suma `Mixtas` a hembras por seguridad: una reserva mixta ignorada sería saldo
  fantasma.
- **No se recorta a cero**: igual que el alimento, un disponible negativo es la señal de que se separó
  de más.

---

## 6. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | Sin reservas | `DisponibleAves(saldo, 0) == saldo` |
| 2 | Saldo 1000, reserva 40 | 960 |
| 3 | Reserva mayor que el saldo | negativo, sin recorte |
| 4 | Saldo que YA incluye las bajas sin validar | **no** se resta otra vez |
| 5 | Producción con reserva activa | `AvesHActual` baja en el disponible, el maestro no |
| 6 | Empresa con flag OFF | idéntico a hoy |

---

## 7. Validación

- `dotnet build` + `dotnet test`.
- Smoke: no reproducible hoy contra datos reales (ninguna empresa con el flag ON tiene lotes de
  postura). Se cubre con tests unitarios y queda dicho acá y en el tracker: **no se declara un smoke
  que no se corrió**.
- Backend propio en `:5501` apagado al terminar.
