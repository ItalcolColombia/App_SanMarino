# La hora de llegada manda el primer día de registro/consumo (engorde Panamá + Ecuador)

> Ticket de operación (Panamá, pollo engorde). Reportan que informando la **hora de encasetamiento**
> tardía —23:58 en la captura del ticket— el módulo **igual muestra un registro el día del encaset**,
> con **saldo de alimento negativo** (−150 kg) porque el alimento entra al día siguiente. Piden además
> que **el mismo comportamiento aplique en Ecuador**.

## 1. Estado real medido (copia de producción, 28-ago-2026)

La regla ya existe desde jul-2026 (`EncasetamientoCalculos`, corte **13:00 inclusive**), gateada por
`companies.primer_registro_segun_hora_llegada` (**solo ItalcolPanama**).

| Hecho medido | Valor |
|---|---|
| Lotes engorde con hora informada | 18 · **16 son ItalcolEcuador**, 2 ItalcolPanama |
| Todas las horas informadas | **≥ 13:00** (14:00, 17:00, 13:30, 22:40, 23:30…) |
| Lotes reproductora aves engorde con hora informada | **0 de 138** (ninguna empresa) |
| Registros manuales que violan la regla, 60 días | **0** — los 4 guardas C# aguantan |
| Registros de **cruce** que la violan | **3**, todos ItalcolPanama (lotes 215 y 216) |
| Lotes Ecuador con hora tardía que ya violarían la regla | **0** ⇒ encender la regla ahí no traba nada |

### 1.1 Los dos defectos

> **Confirmado sobre el lote del ticket.** La copia local tiene el lote **238 «PRUEBA - 1»**
> (ItalcolPanama, encaset 27-ago, **hora 23:58**, borrado el 28-ago 14:17). La fila del 27-ago es
> `id 12937`, `origen_cruce = true`, `created_by_user_id = SYSTEM_CRUCE`, mortalidad 3, selección 2,
> consumo 150 kg — **exactamente** la fila de la captura del ticket (Mort 3 · Sel 2 · TOTAL 5 ·
> saldo alimento −150). No es una captura manual: los cuatro guardas C# la habrían rechazado.

**A. El cruce reproductora → engorde es el único escritor que ignora la hora.**
Los cuatro caminos C# (`SeguimientoAvesEngordeEcuadorService` create/update,
`SeguimientoAvesEngordeService` create/update, más los dos PUT de lote y las dos cargas masivas) ya
rechazan una fecha anterior a `PrimerDiaConRegistro`. El que no la mira es
`fn_cruce_reproductora_a_engorde`: fecha destino `v_fecha_enc + d`, con `d` = edad del registro
reproductora. Como **ningún lote reproductora tiene hora informada**, su guarda nunca dispara, el
operario captura la edad 0 allí y el cruce la re-fecha al **día del encaset del lote engorde**.

Esa fila es la del ticket: `origen_cruce = true` ⇒ **solo lectura en la UI** (por eso abren ticket en
vez de borrarla), consumo real sin ingreso de alimento ⇒ **saldo negativo**, y la columna muestra
**Día 0 / Semana 0** porque el front ya aplica bien el desplazamiento.

**B. Ecuador ve la promesa y no la recibe.** El campo *Hora de encasetamiento* y su leyenda —«Desde
las 13:00 el primer registro de seguimiento pasa al día siguiente»— están en el formulario de lote
engorde **sin gate de empresa**. Ecuador la llenó 16 veces, siempre tardía, y el backend la ignora
entera porque el flag está apagado.

## 2. Enfoque

### Cambio 1 — el primer día lo decide la HORA DEL LOTE, no el flag de empresa

`HoraEfectiva(hora, flagEmpresa)` se deja de usar en los **8 puntos de captura del primer día**; pasan
a leer `lote.HoraEncasetamiento` directo.

- **Inerte donde no hay hora:** hora `NULL` ⇒ desplazamiento 0 ⇒ comportamiento byte a byte previo.
  Sanmarino, Demo y Santa Reyes no tienen un solo lote con hora ⇒ **cero cambios**.
- **Panamá:** ya tenía el flag encendido ⇒ **cero cambios**.
- **Ecuador:** sus 16 lotes tardíos pasan a numerar Día 1 en su primer registro (hoy dicen Día 2) y
  el backend empieza a rechazar capturas en el día del encaset. Medido: **0 registros existentes
  quedarían fuera**, así que no traba ningún lote.

**Lo que NO se toca: el día de pesaje.** `PesajeEngordeCalculos` / `diaParaReglaDePesaje` **conservan
el gate por empresa**. Es una decisión ya tomada y documentada: la guía genética de Ecuador está
tabulada por días desde el encaset y mover el día de pesaje la desalinea. Con el gate quieto, el día
de pesaje de Ecuador queda exactamente donde está.

### Cambio 2 — el cruce respeta la hora del lote engorde

Migración EF que reemplaza `fn_cruce_reproductora_a_engorde` (+ espejo en `backend/sql/`):

```sql
v_desp := CASE WHEN v_hora_enc >= time '13:00' THEN 1 ELSE 0 END;  -- corte 13:00 INCLUSIVE
v_fecha_dest := COALESCE(v_fecha_enc + v_desp + d, r.fecha_reg);
```

Se **corre la serie**, no se descarta el día: el cruce mapea **por edad** (repro edad *d* → engorde
`encaset + d`, verificado en el lote 238: su reproductora tiene encaset propio 29-ago y el cruce
re-fecha todo al calendario del engorde). La fecha destino es el único lugar donde se decide el
calendario del lote engorde, así que es donde va la regla. Con hora `NULL` o `< 13:00` ⇒ `v_desp = 0`
⇒ SQL idéntico al actual.

**Dos correcciones que salieron de probarlo contra el dato real:**

1. **El borrado del cruce sale del loop.** Se borraba edad por edad *dentro* del loop; al correr la
   serie, la fila nueva de la edad *d* caía sobre la fila **vieja** de la edad *d+1*, que todavía no
   se había borrado ⇒ el índice único la rechazaba y no entraba **ninguna**. Es la misma trampa de
   «correr fechas +1 choca con el índice único» que ya se conocía. Ahora se borra el cruce entero
   antes del loop.
2. **`ON CONFLICT DO NOTHING` + `RAISE WARNING`.** El día destino puede estar ocupado por un registro
   **manual** en los lotes creados antes de este arreglo. Sin esto el `INSERT` viola el índice único
   por día UTC y **falla la confirmación de reproductora entera**, sin que el operario sepa por qué.
   Esto no es hipotético: **medido, hoy, con la función actual, recalcular el lote 215 ya revienta**
   con `duplicate key value violates unique constraint "ux_seg_diario_aves_engorde_lote_dia_utc"`.
   Es un defecto latente vivo que este cambio también tapa.

### Cambio 3 — tests

`EncasetamientoCalculosTests` ya cubre el cálculo puro. Se agregan los casos de la regla sin gate y
del desplazamiento del cruce.

## 3. Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/EncasetamientoCalculos.cs` | `HoraEfectiva` queda marcada como solo-pesaje |
| `Infrastructure/Services/SeguimientoAvesEngordeEcuador/Funciones/…Crud.cs` (×2) | usar la hora directa |
| `Infrastructure/Services/SeguimientoAvesEngorde/Funciones/…Crud.cs` (×2) | ídem |
| `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` (×2) | ídem |
| `Infrastructure/Services/LoteAveEngordeService.cs` · `LoteReproductoraAveEngordeService.cs` | ídem (diagnóstico retroactivo) |
| `Infrastructure/Services/Migracion/Funciones/MigracionService.Seguimiento*.cs` | ídem (carga masiva) |
| **Migración nueva** `…_FnCruceReproductoraEngordeHoraLlegada` | fn v2 con `v_desp` |
| `backend/sql/fn_cruce_reproductora_a_engorde.sql` | espejo |
| `frontend/…/engorde-comun/funciones/dia-negocio-engorde.funcion.ts` | `desplazamientoPrimerDia` sin `reglaActiva`; `diaParaReglaDePesaje` la conserva |
| `frontend/…/aves-engorde/pages/…` · `…/seguimiento-diario-lote-reproductora/…` | ajustar llamadas |
| `backend/tests/…/EncasetamientoCalculosTests.cs` | casos nuevos |

## 4. Casos de prueba

1. Hora `NULL` ⇒ primer día = encaset, en las 5 empresas (comportamiento previo).
2. Hora `12:59` ⇒ desplazamiento 0. Hora `13:00` ⇒ 1 (corte inclusive).
3. Lote **Ecuador** con hora 14:00: captura en el día del encaset ⇒ **400** con el motivo.
4. Lote Ecuador **sin** hora: día de pesaje idéntico al de hoy (gate de empresa intacto).
5. Cruce con lote engorde hora 23:58: edades 0..7 ⇒ fechas `encaset+1 … encaset+8`; ninguna fila en
   el día del encaset.
6. Cruce con hora `NULL`: fechas idénticas a las de hoy (`encaset+0 … encaset+7`).

## 4.b Resultados medidos (transacción revertida contra la copia de producción)

| Verificación | Resultado |
|---|---|
| `dotnet build` | 0 errores · 0 advertencias |
| `dotnet test` | **3491/3491** (+4 nuevos) |
| `yarn build` | OK (solo el warning de bundle budget preexistente) |
| Gate `verificar-sql-llega-por-migracion.js` | OK |
| Lote 238 (hora 23:58) recalculado | serie a **28-ago … 02-sep**; **ninguna fila el 27-ago** ✅ |
| Mismo lote con la hora en `NULL` | vuelve a **27-ago … 02-sep**, `desp = 0` ✅ regresión |
| Recálculo de **todos** los lotes con cruce | **0 filas cambiadas** en los lotes sin hora tardía (331 filas idénticas); solo se mueven los 3 de Panamá con hora ≥ 13:00 |
| Control con la fn ACTUAL | lote 215 **falla hoy** con `duplicate key … ux_seg_diario_aves_engorde_lote_dia_utc` |

## 5. Fuera de alcance — se pide OK aparte

- **Remediar los registros de cruce ya torcidos (lotes 215 y 216, ItalcolPanama).** Esta migración
  **no recalcula nada**: las filas viejas se quedan donde están hasta que alguien toque su
  reproductora. Cuando eso pase, el cruce se regenerará desde la reproductora (que es su fuente de
  verdad) y en el 215 quedará con **5 filas en vez de 7** — su edad 0 ya no existe en reproductora y
  las edades 6 y 7 chocan con registros manuales, que las cubren. Hoy ese mismo recálculo **aborta
  con error**, así que el cambio mejora el estado; aun así conviene decidir a mano qué se hace con
  esos dos lotes antes de que un operario los toque.
- **Encender `primer_registro_segun_hora_llegada` en Ecuador.** Con el Cambio 1 **ya no hace falta**
  para lo que pidió el usuario; el flag queda únicamente para el corrimiento del día de pesaje.
