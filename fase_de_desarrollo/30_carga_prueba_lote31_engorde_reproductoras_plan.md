# Plan — Carga de prueba Lote 31 «Doña María D-1» (Excel → BD)

## Objetivo
Validar el flujo completo **lote pollo engorde → lotes reproductora → seguimiento diario 7 días → cruce automático** usando los datos reales del Excel `Libro1.xlsx` (Tabla de desempeño pollo de engorde, granja Doña María D-1, lote #31), cargados sobre la granja ya existente en el sistema (Panamá).

## Datos del Excel (fuente)
- **Lote #31** · Fecha encasetamiento **2026-05-28** · **56,355 aves** · Encargado: Euri Segundo · Alimento: «pre inicio» (ingreso 264.81 qq, boleta 187126).
- **4 bloques de pollitos primera semana** (en la app = **2 lotes reproductora**, H y M juntos):

| Lote reproductora (app) | Hembras | Peso H (g) | Machos | Peso M (g) |
|---|---|---|---|---|
| 34 (H-34 + M-34) | 14,595 | 38.63 | 14,096 | 39.00 |
| 32 (H-32 + M-32) | 13,995 | 39.99 | 13,669 | 40.22 |
| **Total** | **28,590** | | **27,765** | → 56,355 ✓ |

- Días 1–6 con datos reales (consumo qq, mortalidad, selección, agua, calidad de agua); **día 7 (2026-06-04) en cero** en el Excel (solo agua 2,035 L).

## ⚠️ Discrepancias detectadas DENTRO del Excel (tabla principal vs bloques por lote)
1. **Consumo día 1:** tabla principal = **14 qq**; suma de bloques (3+4+4+4) = **15 qq**. (Total acum: 158 vs 159.)
2. **Selección día 4:** tabla principal = **16**; suma de bloques (5+0+5+5) = **15**. (Acum: 46 vs 45; saldo final 56,066 vs 56,067.)
→ La carga usa los **bloques por lote** (fuente granular). El usuario debe confirmar cuál es el dato correcto.

## Contrato validado (EL CÓDIGO MANDA)
- `lote_ave_engorde`: company_id=5 (ItalcolPanama), granja 87 / núcleo '612229' / galpón 'G0447', raza **ROSS AP**, año tabla **2026** (API guia-genetica-ecuador), técnico «panama admin», pais desde `paises` (Panamá).
- `lote_reproductora_ave_engorde`: H/M y AvesInicioH/M se llenan iguales (servicio `CreateAsync`); `reproductora_id` único por lote (formato app: `LR-…`, editable).
- `seguimiento_diario_lote_reproductora_aves_engorde`: 1 registro/día por lote reproductora con **H y M juntos** (`consumo_kg_hembras`, `consumo_kg_machos`, `mortalidad_*`, `sel_*`). Máx **7 registros** (servicio lo valida; con 7/7 el lote queda **Cerrado**).
- **Conversión oficial de la app: 1 QQ = 45.36 kg** (`ModalSeguimientoReproductoraComponent.QQ_TO_KG`), redondeo a 3 decimales. (El Excel usa 45.3597 — diferencia ~0.001%, gana la app.)
- **Trigger `trg_cruce_reproductora_engorde`** (BD): cada INSERT en el seguimiento reproductora regenera el consolidado en `seguimiento_diario_aves_engorde` (edad 1–7; solo si TODOS los lotes reproductora tienen esa edad; agua = la del primer lote, NO se suma).
- `qq_hembras`/`qq_machos`: la app NO los llena al crear desde el modal → quedan NULL; los qq originales se documentan en `observaciones`.
- **Inventario:** el descuento lo hace el backend C# al crear vía API — un INSERT SQL **NO descuenta inventario**. Para validar inventario, registrar por el módulo (tabla qq→kg provista) o cargar el ingreso/consumo por el módulo de inventario.

## Entregables
1. `backend/sql/carga_prueba_lote31_dona_maria.sql` — DO block transaccional (DML puro):
   - Guard anti-duplicado (aborta si ya existe «Lote 31» en company 5) + bloque de limpieza comentado para re-ejecutar.
   - INSERT lote_ave_engorde (hembras 28,590 / machos 27,765 / encasetadas 56,355; pesos ponderados H 39.30 / M 39.60).
   - INSERT 2 lote_reproductora_ave_engorde (34 y 32).
   - INSERT 7 × 2 seguimientos diarios (días 1–7; día 7 en ceros, cierra ambos lotes 7/7). Agua/calidad solo en lote 34 (el cruce toma el primero).
   - Queries de verificación: conteos, cruce generado (7 filas `origen_cruce`), sumas vs Excel (mort 243, sel 45, consumo 159 qq = 7,212.24 kg).
2. Tabla **qq → kg (45.36)** por día y lote para digitación manual en el módulo.

## Casos de prueba
1. Ejecutar el script en BD local → COMMIT sin errores.
2. `seguimiento_diario_aves_engorde` tiene 7 filas `origen_cruce=true` del lote nuevo; día 1 = mort H 20 / M 24 (44 ✓), consumo total 680.40 kg (15 qq).
3. UI lote engorde → detalle: 2 lotes reproductora **Cerrado** 7/7, «Recogida de 7 días ✓», aves actuales 56,067.
4. Módulo seguimiento diario reproductora: los 7 registros visibles por lote; el 8º intento es rechazado por la API.
