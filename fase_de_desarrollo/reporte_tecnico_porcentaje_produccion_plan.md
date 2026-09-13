# Reporte Técnico (Producción): «%Postura» por encima de 100 % → «%Producción» ave-día

**Ticket:** «Los parámetros de porcentajes de producción aparecen por encima de 100 %. Revisar cálculo
para que muestre valor real que se pueda comparar con la guía. […] el parámetro que denominan %Postura
[…] por ser % no debe pasar de 100 y por favor nombrarlo como "%Producción", es el término que manejamos
en reproductoras.»

**Pantalla:** `/reportes-tecnicos` → Producción → Consolidado → **Semanal General** (lote P-K345A, NIZA III).

---

## 1. Diagnóstico (medido, no asumido)

El % se arma en `ReporteTecnicoProduccionService.Tabs.cs` en cuatro lugares:

| Vista | Fórmula actual | ¿Correcta? |
|---|---|---|
| Diario por galpón (l. 374) | huevos del día / hembras vivas × 100 | ✅ |
| Semanal por galpón (l. 430) | promedio del % diario (días con hembras) | ✅ (≤ 100) |
| Diario General (l. 500) | Σ huevos del día / Σ hembras × 100 | ✅ |
| **Semanal General (l. 548)** | **Σ huevos de la SEMANA / hembras al CIERRE × 100** | ❌ |

La Semanal General divide **7 días de huevos** entre **las aves de un solo día** ⇒ el número sale ~7×
inflado. Contraste con la captura de pantalla (prod):

- Semana 2: 19.213 / 7.586 × 100 = **253,3 %** (lo que muestra) — real ≈ 19.213 / (7.590 × 7) = **36,2 %** (guía 31,3).
- Semana 5: 46.035 / 7.526 × 100 = **611,7 %** — real ≈ **87,2 %** (guía 86,8).

**Réplica sobre la BD local** (`fn_seguimiento_diario_produccion(7)`, P-K345A, 7.597 hembras iniciales):

| Sem | Días | Huevos | Saldo fin | Actual (Consolidado) | Corregido ave-día | Prom. diario galpón |
|---|---|---|---|---|---|---|
| 2 | 7 | 15.794 | 7.588 | 208,1 | **29,7** | 29,7 |
| 5 | 7 | 46.060 | 7.530 | 611,7 | **87,2** | 87,2 |
| 9 | 7 | 44.805 | 7.330 | 611,3 | **87,2** | 87,2 |
| 17 | 7 | 36.437 | 7.163 | 508,7 | **72,6** | 72,6 |

(Los números locales difieren un poco de prod porque la BD local es un dump anterior; el patrón es el mismo.)

**Referencia canónica:** `fn_indicadores_produccion_postura.sql:340` ya define
*«%Producción hen-day = huevos/día / HEMBRAS vivas × 100»*. La Semanal General es la única que se aparta.

## 2. Enfoque

**Una sola fórmula por número** → cálculo puro en `Application/Calculos/PorcentajeProduccionCalculos.cs`:

- `Diario(huevos, hembras)` = huevos / hembras × 100 (0 sin hembras) — aritmética idéntica a la actual.
- `Periodo(días)` = Σ huevos / Σ aves-día × 100, donde aves-día = hembras vivas de cada día registrado.
  Los días sin hembras (saldo 0) no entran ni en el numerador ni en el denominador (mismo criterio que
  ya aplicaba el promedio semanal por galpón).

Con una base de aves estable, `Periodo` coincide con el promedio del % diario (medido: igual a 1
decimal en las 22 semanas locales), y **pondera por aves** cuando se consolidan varios galpones, que
es lo correcto para un consolidado.

Consumidores:
1. Diario por galpón → `Diario` (sin cambio numérico).
2. Semanal por galpón → `Periodo` sobre los días de la semana (diferencia < 0,05 pts, ver §1).
3. Diario General → `Periodo` sobre los galpones del día (sin cambio salvo el caso sin sentido de un galpón con 0 hembras y huevos cargados).
4. **Semanal General → `Periodo` sobre TODOS los días de TODOS los galpones de esa semana** (el fix).

`DifPostura` y el semáforo del front salen de ese mismo número ⇒ se corrigen solos. El Excel
(`ExportacionExcelService`) lee los DTO ⇒ se corrige solo.

## 3. Rótulo «%Postura» → «%Producción»

Cambio global (no por flag): «% producción» es el nombre de la métrica en todas las guías
(`prod_porcentaje`, incluida la de postura comercial), así que no hay empresa para la que el rótulo
nuevo sea incorrecto.

- Front `features/reportes-tecnicos/components/`: los 4 `<th>` de `reporte-diario-galpon`,
  `reporte-semanal-galpon`, `reporte-general-diario`, `reporte-general-semanal` (+ sus doc-comments).
- Back `ExportacionExcelService.cs`: 5 encabezados `"%Postura"` → `"%Producción"` y 2 `"Postura Guía"` → `"Producción Guía"`.
- Modal «Fórmulas» del reporte: se agrega la fórmula de %Producción para que el usuario vea cómo se compara con la guía.
- **No** se renombran propiedades de DTO (`porcentajePostura*`, `DifPostura`): es contrato API; cambiarlo no aporta al ticket y rompe consumidores.

## 4. Sin cambios de BD

Ni migración ni SQL. Solo C# + Angular.

## 5. Casos de prueba (xUnit `PorcentajeProduccionCalculosTests`)

1. `Diario`: 3.705 huevos / 7.596 hembras = 48,78 %; con 0 hembras → 0.
2. **Regresión del ticket:** semana de 7 días, 19.213 huevos, saldo 7.595→7.586 ⇒ 36,16 % (la fórmula vieja daba 253,3).
3. `Periodo` de un solo día == `Diario`.
4. Con base de aves constante, `Periodo` == promedio del % diario (equivalencia con la regla anterior del semanal por galpón).
5. Consolidado de 2 galpones pondera por aves: (900/1.000) + (2.100/3.000) ⇒ 75 %, no 80 %.
6. Días con 0 hembras no cuentan; período vacío → 0.
7. Si cada día huevos ≤ hembras, el resultado nunca pasa de 100.

## 6. Validación

- `dotnet build` (0 err, sin warnings nuevos) + `dotnet test` Application.Tests.
- `yarn build` (0 err; solo el warning de budget preexistente).
- Smoke: réplica SQL (§1) = valores esperados del endpoint.
