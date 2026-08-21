# Ganancia diaria (g) — seguimiento diario pollo engorde: dividir por los días reales entre pesajes

## Origen

Validación de Lady Malave (ecuitalcol) sobre el módulo de indicadores del seguimiento diario de
pollo engorde (tabla "Ecuador mixto"): peso corporal, alimento diario, alimento acumulado,
conversión y mortalidad+selección están OK. **Ganancia diaria está mal** cuando el pesaje deja de
ser diario: la 1ª semana se pesa los 7 días, luego cada 4 días, y la fórmula debe pasar de
`(peso día − peso día anterior)` a `(peso día − peso día anterior) / 4` en esos tramos.

Recomendación validada por Moises para generalizar la regla (no asumir siempre 4 días fijos):
comparar el peso actual contra **el último peso efectivamente registrado antes de ese**, dividiendo
por los días reales transcurridos entre ambos pesajes (no un divisor fijo).

## Diagnóstico (código actual)

Único cálculo real de esta tabla: [`indicadores-diarios-engorde-compute.service.ts`](../frontend/src/app/features/engorde-comun/services/indicadores-diarios-engorde-compute.service.ts)
(`engorde-comun`, fuente única — `aves-engorde/services/indicadores-diarios-engorde-compute.service.ts`
es un shim `export *` hacia este archivo, no hay una segunda implementación que arreglar).

El servicio **ya** compara contra el último peso realmente registrado (variable `ultimoPesoMedido`,
que solo se actualiza cuando `pesoReal > 0`) — eso ya resuelve la parte de "comparar contra el
último pesaje, no contra el día calendario anterior". Lo que falta es **dividir ese delta entre los
días transcurridos** desde ese último pesaje: hoy el código hace `pesoReal - ultimoPesoMedido` a
secas, sin dividir, así que en un tramo de pesaje cada 4 días la tabla muestra la ganancia
*acumulada de 4 días* como si fuera la de un solo día (columna "Registro" muy por encima de la
columna "Guía").

## Fix

En el mismo archivo: se agrega `ultimoPesoDia` (día de vida del último pesaje real, arranca en 0 =
día del encasetamiento, que es cuando se toma `pesoIni`) junto a `ultimoPesoMedido`, y la ganancia
divide por `Math.max(1, dia - ultimoPesoDia)`:

```ts
let gananciaReal: number | null = null;
if (pesoReal > 0) {
  const diasTranscurridos = Math.max(1, dia - ultimoPesoDia);
  gananciaReal = (pesoReal - ultimoPesoMedido) / diasTranscurridos;
}
...
if (pesoReal > 0) {
  ultimoPesoMedido = pesoReal;
  ultimoPesoDia = dia;
}
```

- **1ª semana (pesaje diario):** `dia - ultimoPesoDia == 1` siempre ⇒ dividir entre 1 no cambia el
  resultado. Cero impacto en el tramo que Lady confirmó como correcto.
- **Tramos de pesaje cada 4 días:** el delta se reparte en 4, quedando comparable contra la guía
  (columna "Guía" ya es un valor diario).
- **Generaliza a cualquier intervalo** (no asume 4 fijo): si algún día se salta el pesaje por otro
  motivo, igual divide entre los días reales transcurridos — es la generalización de la
  recomendación de Moises.
- Días sin peso (`pesoReal == 0`): sigue devolviendo `gananciaReal = null` (la UI ya pinta "—"),
  sin tocar `ultimoPesoMedido`/`ultimoPesoDia` — comportamiento preexistente intacto.

No hay cambio de esquema/BD: el cálculo es 100% frontend (Angular), no hay endpoint ni función SQL
involucrada en esta tabla.

## Archivos

- `frontend/src/app/features/engorde-comun/services/indicadores-diarios-engorde-compute.service.ts` (fix)
- `frontend/src/app/features/engorde-comun/services/indicadores-diarios-engorde-compute.service.spec.ts` (nuevo — tests)
- Migración de datos (módulo Tickets): registra el caso de Lady Malave con la solución aplicada,
  para que ella pueda cerrarlo desde la pantalla. Sigue el patrón de
  `20260819120000_SeedTicketPlanItalappSantaReyes` (resolución por email/nombre, fail-open,
  idempotente por `titulo`).

## Casos de prueba (spec)

1. Pesaje diario (días 1..7, delta constante): ganancia = delta sin dividir (divisor 1, regresión
   cero contra el comportamiento que Lady confirmó OK).
2. Pesaje cada 4 días tras la 1ª semana: ganancia = delta / 4 (caso reportado, antes daba el delta
   sin dividir).
3. Intervalo distinto de 4 (p.ej. salto de 3 o 5 días): ganancia = delta / días reales — cubre la
   generalización pedida.
4. Día sin peso registrado (`pesoPromH`/`pesoPromM` en 0/null): `gananciaDiariaRealG` es `null` y
   NO mueve el acumulador de "último pesaje".
5. Primer pesaje del lote contra el peso inicial (`pesoIni`, día 0): usa `dia - 0` como divisor.

## Validación

- `cd frontend && yarn build` (0 errores).
- Test dirigido del servicio (Jasmine/Karma vía `yarn test`, o ejecución aislada si el runner
  completo es muy largo).
- Migración: validar el SQL por transacción (`BEGIN; ...; ROLLBACK;`) contra la BD local antes de
  dejarla para aplicar, corriéndolo dos veces para confirmar idempotencia.
