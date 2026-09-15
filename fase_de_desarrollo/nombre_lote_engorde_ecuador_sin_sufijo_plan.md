# Plan — Nombre del lote de engorde en Ecuador SIN sufijo de corrida (14-sep-2026)

## Novedad (Ecuador, Kilometro 22)

En el filtro Granja → Núcleo → Galpón → Lote de **Galpon-1** aparece `2604 - 2 - ERP: 316-40202604`
y en el consumo se ven "dos lotes" (`2604` y `2604 - 2`).

## Diagnóstico (copia de prod del 14-sep)

- No hay lote duplicado: el lote base 2604 tiene **un lote vivo por galpón**, igual que 2601-2603
  (`2604` en Galpon-2 id 220, `2604 - 2` en Galpon-1 id 240).
- El `- 2` sale de `LoteAveEngordeService.CreateAsync`: corrida = `MAX(numero_corrida)` por
  empresa + base + galpón **contando lotes borrados**, y con `nombre_lote_incluye_corrida = false`
  (Ecuador) `ConstruirNombreLote` pone el sufijo desde la 2.ª corrida.
- En los 4 casos vivos hubo antes un lote de prueba **borrado sin seguimientos** en el mismo galpón
  (213 en Galpon-1 de Kilometro 22, borrado a los 36 s; 196, 235 y 236 en CAROLINA).

| id | Granja | Galpón | Nombre actual | Corrida | Queda |
|---|---|---|---|---|---|
| 240 | Kilometro 22 | Galpon-1 (G0035) | 2604 - 2 | 2 | 2604 / 1 |
| 218 | CAROLINA | GALPON 1 (G0057) | 2604 - 2 | 2 | 2604 / 1 |
| 237 | CAROLINA | GALPON 6 (G0062) | 2604 - 2 | 2 | 2604 / 1 |
| 247 | CAROLINA | GALPON 5 (G0061) | 2604 - 2 | 2 | 2604 / 1 |

## Decisión del usuario

En Ecuador **no aplica** la regla del sufijo: el nombre del lote es SIEMPRE el nombre del lote base.
Se corrige el código y los datos, con migración para desplegar.

## Enfoque

Flag existente `companies.nombre_lote_incluye_corrida` (no se crea ni se borra columna):

| | Flag ON (Panamá) | Flag OFF (Ecuador) |
|---|---|---|
| Nombre | `{base} - {n}` (sin cambio) | `{base}` siempre |
| Corrida (referencia) | MAX incluye borrados (sin cambio) | MAX solo de lotes vivos |
| Guarda | ninguna (sin cambio) | no abrir el mismo base en un galpón donde ya hay uno **vivo y no cerrado** |

La guarda reemplaza lo que hacía el sufijo: sin ella podrían quedar dos lotes abiertos con el mismo
nombre en el mismo galpón. Solo aplica al flujo interactivo (`AutoNombrePorCorrida`); Puente Panamá y
migración masiva no pasan por ahí.

## Archivos

- `Application/Calculos/GestionLotesEngordeCalculos.cs`: `ConstruirNombreLote` (OFF ⇒ base),
  `CorridaCuentaLotesBorrados`, `ValidarAperturaLoteBase` + mensaje.
- `tests/.../GestionLotesEngordeCalculosTests.cs`: casos OFF actualizados + guarda + Panamá intacto.
- `Infrastructure/Services/LoteAveEngordeService.cs` (`CreateAsync`): resuelve flag primero, filtra
  borrados solo con OFF, aplica la guarda.
- `frontend/.../lote-engorde-list.component.ts` (`recomputeNombrePorCorrida`): preview sin sufijo con OFF.
- Migración data-only `20260914153000_NombreLoteEngordeSinSufijoCorrida` (Designer = snapshot, sin
  tocar ModelSnapshot).
- `backend/sql/verificar_nombre_lote_engorde_sin_sufijo_corrida.sql` (solo lectura).

## Migración (datos)

Empresas con flag OFF, lotes **vivos** con base + galpón + corrida cuyo nombre es exactamente
`{base} - {numero_corrida}` (lo escribió el sufijo automático):

1. `lote_nombre = {base}`, `numero_corrida` = posición entre los vivos del mismo base+galpón.
   Se salta si ya hay otro lote vivo con ese nombre en el galpón (no crear homónimos).
2. Etiqueta de los gastos de esos lotes: `Gasto inventario #N fecha · Lote 2604 - 2` →
   `· Lote 2604` en `inventario_gestion_movimiento.reference` y
   `lote_registro_historico_unificado.referencia`. Es texto de pantalla: ningún lector lo parsea (la
   clave de lectura es el prefijo `Gasto inventario #N`, que no cambia). Sin triggers sobre esas columnas.

Idempotente: tras la 1.ª pasada ningún nombre cumple el patrón ⇒ `UPDATE 0`. Down sin reversa.
`liquidacion_lote_engorde_congelada` no se toca (los 4 lotes están abiertos; sus 7 filas con ` - ` son de Panamá).

## Casos de prueba

- Calc OFF: base `2603`, corrida 1/2/3 ⇒ `2603`; recorta espacios; base nulo no rompe.
- Calc ON: `96 - 1`, `96 - 2` (idéntico a antes).
- `CorridaCuentaLotesBorrados`: ON true, OFF false.
- Guarda: OFF + abierto ⇒ mensaje con el base; OFF sin abierto ⇒ null; ON + abierto ⇒ null.
- BD local en `BEGIN…ROLLBACK`: 4 lotes renombrados, 5 + 5 referencias, 0 Panamá; 2.ª pasada 0.
- `dotnet build` 0/0, `dotnet test`, `verificar-sql-llega-por-migracion.js`, `yarn build`.
