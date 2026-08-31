# Día de encasetamiento: numeración 1-based en indicadores y herencia de hora en reproductora

**Ticket (Panamá, 31-ago-2026):** lote encasetado el 27-ago aparece con su primer registro (28-ago)
como **«día 2»** en seguimiento reproductora pollo engorde; y los **indicadores diarios de engorde**
siguen mostrando **«día 0»** en el primer día. Regla del negocio: **no existe el día cero — el primer
día con registro es el día 1**.

## Diagnóstico (verificado contra la copia de prod, BD local `sanmarinoapplocal:5433`)

Caso del ticket: lote engorde **239 «95 - 1»** (ItalcolPanama, encaset 27-ago, **hora 21:33** ⇒
llegada tardía) con reproductoras **146 «35»** (LR-8229084044) y **147 «155»**; único seguimiento el
28-ago (edad 1). La regla del «día de negocio» (`edad − desplazamiento + 1`, 27-jul/28-ago-2026) ya
existe en `EncasetamientoCalculos` + `dia-negocio-engorde.funcion.ts` y ya rige la tabla de
seguimiento de engorde y el cruce (fn). Quedaron DOS huecos:

1. **Indicadores diarios de engorde** (`IndicadoresDiariosEngordeComputeService`): `row.dia` es la
   edad 0-based cruda (`calcularDiaVida`) y se pinta tal cual en tabla, CSV y gráficas → «día 0».
2. **Seguimiento reproductora**: TODA la cadena (numeración de la lista, fecha sugerida, min del
   modal, guardas de Create/Update, carga masiva) usa `lote_reproductora_ave_engorde.hora_encasetamiento`,
   que está **NULL en 142 de 142** filas — la hora se captura en el formulario del **lote POLLO
   ENGORDE**. Nadie hereda esa hora ⇒ desplazamiento 0 ⇒ el 28-ago sale como «día 2» y el guarda
   nunca dispara.

Mediciones que acotan el cambio:
- **Ningún** lote de engorde con hora ≥ 13:00 tiene seguimiento en la edad 0 (18 lotes revisados:
  16 Ecuador «2604» siempre arrancaron al día siguiente; Panamá 215/216 re-fechados por la
  remediación `20260828200000`). ⇒ En engorde la fórmula pura nunca produce día ≤ 0.
- En reproductora, **solo 131 («35» del lote 215) y 132 («35» del 216)** capturaron su semana
  arrancando en la edad 0 siendo tardíos (pre-guarda). Semana completa (7/7) y confirmada; el cruce
  ya la re-fechó bien en engorde (verificado en UTC: filas 11..16-ago y 14..19-ago).
- Falsa alarma descartada: leer `fecha::date` en sesión -05 desplaza un día las filas de cruce
  (ancladas a medianoche UTC); en UTC la remediación del 28-ago está correcta.

## Enfoque

**Cero DDL, cero migraciones, cero movimientos de datos.** Todo es presentación + resolución de la
hora efectiva. Mover las fechas de 131/132 se descarta explícitamente: un UPDATE re-dispararía el
trigger de cruce (DELETE+INSERT re-fechado) sobre días ya ocupados por registros manuales y chocaría
con el índice único `(lote, fecha)`.

### Backend
- `Application/Calculos/EncasetamientoCalculos.cs`: nuevo `HoraEfectivaReproductora(horaReproductora,
  horaLoteEngorde)` = `horaReproductora ?? horaLoteEngorde` (la reproductora es la misma llegada
  física que su lote de engorde; la hora se captura en el form del engorde).
- `Application/DTOs/LoteReproductoraAveEngordeDto.cs`: campo nuevo `TimeOnly?
  HoraEncasetamientoEfectiva` (el form CRUD sigue editando la propia; las vistas de numeración usan
  la efectiva — no se materializa la heredada).
- `LoteReproductoraAveEngordeService`: proyectar `lae.HoraEncasetamiento` en GetAll/GetById/Create/
  CreateBulk/Update/Reabrir y pasarla a `Map`; el diagnóstico retroactivo del Update usa la efectiva.
- `SeguimientoDiarioLoteReproductoraService`: guardas de Create y Update validan con la hora
  efectiva (el Create ya joinea el lote engorde; al Update se le agrega la proyección).
- `MigracionService.SeguimientoReproductora`: `ReproductoraInfo` + `HoraLoteEngorde`; la validación
  de fecha usa la efectiva.
- Tests xUnit en `EncasetamientoCalculosTests`: herencia (propia gana / hereda del engorde / ambas
  null / heredada temprana no desplaza).

### Frontend
- `lote-reproductora-ave-engorde.service.ts`: `horaEncasetamientoEfectiva?: string | null`.
- `seguimiento-diario-lote-reproductora-list`:
  - desplazamiento desde la hora **efectiva** y **acotado a la menor edad registrada**
    (`min(desplazamientoHora, edadMinimaConRegistro)`): 146/147 muestran día 1..7 desde la edad 1,
    y los históricos 131/132 (edades 0..6) conservan 1..7 — ninguna fila queda sin número;
  - `nextSuggestedFecha` del primer registro = encaset + desplazamiento;
  - el modal recibe la hora efectiva (su fecha mínima ya la respeta).
- `IndicadoresDiariosEngordeComputeService.compute(..., horaEncasetamiento?)`: `row.dia` pasa a ser
  el **día de negocio 1-based**; la **edad interna queda intacta** para el cruce con la guía
  genética y para la aritmética de ganancia diaria (misma salida numérica).
- `tabla-indicadores-diarios-engorde` y `graficas-indicadores-diarios-engorde`: `@Input()
  horaEncasetamiento` → compute; `tabs-principal-engorde.html` se la pasa (ya la tiene).
- Specs Karma del compute: numeración +1, caso «día del encaset = día 1» y caso «lote tardío
  arranca en día 1».

## Casos de prueba
1. xUnit: `HoraEfectivaReproductora` (4 casos).
2. Karma compute: registros edades 0..6 sin hora ⇒ días 1..7; edad 1 con hora 21:33 ⇒ día 1; guía
   del día sigue siendo la de la edad (fila día 1 sin hora usa guía día 0).
3. Smoke HTTP local (backend :5002, BD copia de prod):
   - `GET /api/LoteReproductoraAveEngorde/146` → `horaEncasetamientoEfectiva == "21:33:00"`.
   - `POST /api/SeguimientoDiarioLoteReproductora` con fecha 27-ago en el lote 146 → **400** con
     «El primer registro de este lote es el 2026-08-28…».
   - `GET /api/LoteReproductoraAveEngorde/131` → efectiva 23:30 (numeración front acotada 1..7).
4. Empresa sin hora (Sanmarino/Demo): efectiva null ⇒ byte a byte como antes (desplazamiento 0).

## Validación
- `dotnet build` 0/0 + `dotnet test` verdes; `yarn build` OK + spec del compute verde.
- Sin fn SQL tocada ⇒ no aplica el gate de paridad de saldos multipaís; sin `.sql` nuevos ⇒ el gate
  de migraciones no cambia.
- Backend de smoke se apaga al terminar (puerto :5002 libre).
