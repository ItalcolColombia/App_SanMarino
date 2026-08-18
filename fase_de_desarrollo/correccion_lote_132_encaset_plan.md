# Corrección del encaset del lote 132 — el último sin referencia confiable

**Fecha:** 2026-08-18 · **Decisión del usuario:** corregir a 19.187 (bloque V25.6.3 del tracker)
**Precedente a copiar:** `20260805170000_CorreccionInicioHistorialYEncasetEngorde` (lote 30)

## 1. Enfoque y por qué

El lote 132 (ItalcolEcuador · Sacachun 3b · Galpon-3 · «2604») es el **único de los 186** de la base
con `referencia_confiable = false`, y el único que no cuadra. Medido:

| Dato | Valor |
|---|---|
| `Inicio` del historial (id 180, 21-jul-2026) | 8.414 H + 10.773 M + 0 mixtas = **19.187** |
| `lote_ave_engorde.aves_encasetadas` | **19.387** ⇒ gap de **200** |
| Maestro hoy (`hembras_l` / `machos_l`) | 8.329 / 10.393 |
| Esperado por la fn (`Inicio − ventas − bajas − ajustes`) | 8.129 / 10.393 |
| Desfase | **200 H** / 0 M |
| Bajas (`BAJA_SEGUIMIENTO`, no anuladas) | 285 H / 380 M · **sin ventas** |

Las dos discrepancias son **la misma**: el lote se creó con 200 hembras de más
(8.614 − 285 bajas = 8.329, el maestro de hoy) y el `Inicio` registró el número real. Por eso
`fn_cuadre_aves_engorde` marca `confiable = false` — su predicado exige
`Inicio_h + Inicio_m + Inicio_x = aves_encasetadas`.

**Se corrige hacia el `Inicio`, no al revés.** El `Inicio` es el registro del acto de encasetamiento;
`aves_encasetadas` es un campo del maestro que ya se demostró editable y sujeto a inflado (fue
exactamente la causa del lote 30). Corregir el `Inicio` para que empate con un maestro inflado sería
mover la evidencia para que coincida con el error.

**Alternativa descartada:** dejarlo y documentarlo. La descarta el usuario, y además deja la base con
1 lote fuera de toda auditoría de conservación de forma permanente.

## 2. Archivos

- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_CorreccionEncasetLote132.cs` — data-only,
  Designer **clonado del último real**, **ModelSnapshot intacto**
- `backend/sql/correccion_encaset_lote_sin_referencia_confiable.sql` — el mismo SQL, trazable
- `backend/src/ZooSanMarino.Application/Calculos/CuadreAvesEngordeCalculos.cs` — **NUEVO**: la regla
  de detección como función pura, para poder testearla sin BD
- `backend/tests/ZooSanMarino.Application.Tests/CuadreAvesEngordeCalculosTests.cs` — **NUEVO**

## 3. Cambios de BD — regla dinámica, sin nombrar el id

```sql
-- Alcance: lotes cuyo Inicio NO empata con aves_encasetadas Y cuyo gap es exactamente el desfase
-- del maestro. Ambas condiciones juntas: el gap se explica por el maestro inflado, no por otra causa.
WITH ini AS (
  SELECT DISTINCT ON (h.lote_ave_engorde_id) h.lote_ave_engorde_id AS id,
         COALESCE(h.aves_hembras,0) AS ih, COALESCE(h.aves_machos,0) AS im, COALESCE(h.aves_mixtas,0) AS ix
  FROM historial_lote_pollo_engorde h
  WHERE h.tipo_lote='LoteAveEngorde' AND h.tipo_registro='Inicio' AND h.lote_ave_engorde_id IS NOT NULL
  ORDER BY h.lote_ave_engorde_id, h.fecha_registro, h.id)
-- objetivo := ini.ih+ini.im+ini.ix ; desfase := maestro − esperado
-- Se actualiza SOLO si  aves_encasetadas − objetivo = desfase_h + desfase_m  (y ambos ≥ 0)
```

- `aves_encasetadas` ⇒ el total del `Inicio` (19.187)
- `hembras_l` ⇒ `hembras_l − desfase_h` (8.329 → 8.129)
- `machos_l` ⇒ `machos_l − desfase_m` (10.393, sin cambio)
- **Idempotente:** `WHERE ... IS DISTINCT FROM` ⇒ la 2ª corrida da `UPDATE 0`
- **NO se toca `historial_lote_pollo_engorde`**: el `Inicio` ya es correcto

## 4. Reglas de negocio

- El `Inicio` es la referencia; el maestro se alinea a él, nunca al revés
- La regla no nombra ids: si mañana aparece otro lote con el mismo patrón, lo alcanza
- **Orden:** va ANTES de cualquier cierre. El *Gate B1* impide editar `aves_encasetadas` de un lote
  liquidado, porque invalidaría la copia congelada

## 5. Casos de prueba

1. **Alcance**: la regla toca **exactamente 1 lote** (verificado hoy contra los 186 de la base)
2. **Resultado**: tras aplicar, `fn_cuadre_aves_engorde(NULL)` devuelve **0 sin referencia confiable
   y 0 que no cuadran** (hoy: 1 y 1)
3. **Idempotencia**: 2ª corrida ⇒ `UPDATE 0`
4. **Simulación previa** en transacción + `ROLLBACK`, con el antes/después a la vista
5. xUnit sobre la función pura: caso que cuadra (no toca), caso 132 (corrige), caso donde el gap
   **no** coincide con el desfase (no toca — es otra causa), caso sin `Inicio` (no toca)
6. **No regresión**: el resto de los 185 lotes queda byte a byte igual

## 6. Riesgos y qué NO hace

- **No decide cuál número es «el verdadero» por evidencia física.** El usuario decidió que es el
  `Inicio` (19.187). El lote está activo y sin ventas, así que la conservación no lo puede probar sola
- No toca el historial, ni las bajas, ni ningún otro lote
- No cierra ni liquida nada
