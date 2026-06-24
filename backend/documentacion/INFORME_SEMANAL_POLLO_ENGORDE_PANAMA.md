# 📅 Informe Semanal Pollo de Engorde (Panamá)

> Reporte nuevo. Estado: **funcional de punta a punta (BD → back → front + menú)**, con las columnas de
> **comparación contra la guía genética ("Tabla") dejadas como placeholder NULL** a la espera de
> definición del cliente. Documento de handoff para continuar la integración.
>
> - Plan: [`fase_de_desarrollo/32_informe_semanal_pollo_engorde_panama_plan.md`](../../fase_de_desarrollo/32_informe_semanal_pollo_engorde_panama_plan.md)
> - Tracker: [`tracker_estado.md`](../../tracker_estado.md)
> - Excel de referencia (cliente): `Libro3 (1).xlsx`, hoja "1 AL 5 DE MAY DEL 2026".

---

## 1. Objetivo

Para una **semana calendario** (rango de fechas), listar cada **lote** de pollo de engorde por su
**semana de vida** (1, 2, 3…), con sus indicadores **reales** (peso, consumo, mortalidad, selección,
ventas, agua) y, en una segunda fase, compararlos contra el **estándar de la guía genética** ("Tabla").
Incluye una fila **CONSOLIDADO** por semana. Filtros: granja (todas / varias / una), núcleo, galpón, lote y rango de fechas.

El layout del Excel agrupa por bloques `SEMANA N.1..N`, con filas por lote + `CONSOLIDADO`. La
implementación devuelve **una fila por (lote, semana de vida)**; el front agrupa por semana y arma el consolidado.

---

## 2. Arquitectura (la BD hace el cálculo)

```
Angular (informe-semanal-engorde)
   └─ POST /api/InformeSemanalPolloEngorde/generar  (filtros)
        └─ InformeSemanalPolloEngordeService  (delgado: arma params, mapea, consolida)
             └─ fn_informe_semanal_pollo_engorde(company, granjas[], nucleo, galpon, lote, desde, hasta)
                  └─ CROSS JOIN LATERAL fn_seguimiento_diario_engorde(lote)   ← lógica diaria ya probada
                       └─ seguimiento_diario_aves_engorde + movimiento_pollo_engorde + lote_ave_engorde
```

El backend casi no tiene lógica: toda la agregación vive en la función SQL (reutiliza
`fn_seguimiento_diario_engorde`, que ya resuelve edad/semana/saldo de aves/consumo/despachos).

---

## 3. Tablas que traen la información (REALES)

| Tabla | Qué aporta | Columnas usadas |
|---|---|---|
| **`lote_ave_engorde`** | Identidad del lote, encaset, peso de llegada | `lote_ave_engorde_id, company_id, granja_id, nucleo_id, galpon_id, lote_nombre, fecha_encaset, aves_encasetadas, hembras_l, machos_l, mixtas, peso_inicial_h, peso_inicial_m, peso_mixto` |
| **`seguimiento_diario_aves_engorde`** | Datos diarios reales | `fecha, mortalidad_hembras, mortalidad_machos, sel_h, sel_m, consumo_kg_hembras, consumo_kg_machos, peso_prom_hembras, peso_prom_machos, consumo_agua_diario` |
| **`movimiento_pollo_engorde`** | Ventas / despachos | `tipo_movimiento ('Venta','Despacho','Retiro'), cantidad_hembras/machos/mixtas, peso_neto, fecha_movimiento, estado, deleted_at` (se accede vía la fn diaria como `despacho_*`) |
| `farms`, `galpones` | Nombres de granja/galpón | `name`, `galpon_nombre` |

> ⚠️ **Nota de país:** existen entidades EF para `seguimiento_diario_aves_engorde_panama` y
> `..._ecuador`, pero en la BD local esas tablas **no existen**: los datos viven en la tabla
> genérica `seguimiento_diario_aves_engorde` (igual que el resto de reportes de engorde). La fn lee
> la genérica. Si en el futuro se separan por país, cambiar la fuente en la fn diaria/semanal.

---

## 4. De dónde sale la "Tabla genética" (placeholder — AÚN NO CONECTADO)

La comparación "Real vs Tabla" del Excel usa el estándar de la **guía genética**. La fuente es:

| Tabla | Columnas relevantes |
|---|---|
| **`guia_genetica_ecuador_header`** | `id, raza, anio_guia, company_id` (UNIQUE company+raza+año) |
| **`guia_genetica_ecuador_detalle`** | `guia_genetica_ecuador_header_id, sexo ('mixto'/'hembra'/'macho'), **dia**, peso_corporal_g, ganancia_diaria_g, promedio_ganancia_diaria_g, cantidad_alimento_diario_g, **alimento_acumulado_g**, ca (conversión), mortalidad_seleccion_diaria` |

Mapeo previsto Excel "Tabla" → guía:

| Columna Tabla (Excel) | Origen en `guia_genetica_ecuador_detalle` |
|---|---|
| CONSUMO Tabla | `alimento_acumulado_g` |
| PESO Tabla | `peso_corporal_g` |
| GANANCIA Tabla | `peso_corporal_g(sem) − peso_corporal_g(sem−1)` |
| CONVERSIÓN Tabla | `ca` |
| MORTALIDAD TABLA | `mortalidad_seleccion_diaria` |

**❓ PREGUNTA ABIERTA (decisión del cliente, por eso NO se conectó):** el detalle es **por día**
(`dia`). Para el valor semanal hay que decidir si:
- se **toma el valor del día = semana·7** (p. ej. peso al día 7, 14, 21…), o
- se **suma los 7 días** (correcto para `alimento_acumulado_g`/consumo, no para peso).

La raza/año por lote sale de `lote_ave_engorde.raza` + `ano_tabla_genetica` → `guia_genetica_ecuador_header`.

### Punto de enganche en el código
En `backend/sql/fn_informe_semanal_pollo_engorde.sql` (y su migración), el bloque final tiene:
```sql
NULL::numeric AS consumo_tabla_g,
NULL::numeric AS peso_tabla_g,
NULL::numeric AS ganancia_tabla_g,
NULL::numeric AS conversion_tabla,
NULL::numeric AS mortalidad_tabla_pct,
NULL::numeric AS pct_consumo,  -- (real/tabla)*100
NULL::numeric AS pct_peso,
NULL::numeric AS pct_conversion
```
Cuando se defina la regla: agregar un CTE `tabla` que una `lote_ave_engorde` → header (raza/año) →
detalle (por `dia`/semana y `sexo`), y reemplazar esos `NULL` por los valores + los `pct_*`.

---

## 5. Función SQL `fn_informe_semanal_pollo_engorde`

Archivo canónico: [`backend/sql/fn_informe_semanal_pollo_engorde.sql`](../sql/fn_informe_semanal_pollo_engorde.sql).
Migración EF: `20260623070401_AddFnInformeSemanalPolloEngorde`.

```sql
fn_informe_semanal_pollo_engorde(
  p_company_id  INT,            -- obligatorio (se toma del usuario)
  p_granja_ids  INT[]  = NULL,  -- NULL/{} = todas; si no, sólo esas
  p_nucleo_id   TEXT   = NULL,
  p_galpon_id   TEXT   = NULL,
  p_lote_id     INT    = NULL,
  p_fecha_desde DATE   = NULL,  -- overlap con el rango de la semana de vida
  p_fecha_hasta DATE   = NULL
) RETURNS TABLE(... 41 columnas ...)
```

**Fórmulas (REALES):**
- `consumo_real_g_ave` = `consumo_acum_kg / aves_encasetadas * 1000` (acumulado por ave, g).
- `peso_real_g` = último peso promedio (H/M) registrado en la semana.
- `ganancia_real_g` = `peso(sem) − peso(sem−1)`; la 1ª semana usa `peso_llegada_g`.
- `conversion_real` = `consumo_real_g_ave / peso_real_g`.
- `mort_natural_pct` / `seleccion_pct` / `mortalidad_total_pct` = `unidades / saldo_inicio_semana * 100`,
  con `saldo_inicio_semana = aves_encasetadas − Σ(mort+sel+ventas de semanas previas)`.
- `ventas_unid` / `ventas_kg` desde despachos (Venta/Despacho/Retiro).
- `relacion_agua` = `agua_ml / consumo_acum_kg`.

> ❓ **A confirmar con el cliente:** el denominador de los % de mortalidad/selección
> (`saldo_inicio_semana` = aves vivas al inicio de la semana). Si el cliente usa aves encasetadas
> iniciales, es un cambio de una línea en la fn.

---

## 6. Backend (.NET)

| Archivo | Rol |
|---|---|
| `Application/DTOs/InformeSemanalPolloEngordeDtos.cs` | `InformeSemanalRequest`, `InformeSemanalRow` (proyección keyless 1:1 de la fn), `InformeSemanalFilaDto`, `InformeSemanalConsolidadoDto`, `InformeSemanalGrupoSemanaDto`, `InformeSemanalReporteDto` |
| `Application/Interfaces/IInformeSemanalPolloEngordeService.cs` | Contrato |
| `Infrastructure/Services/InformeSemanalPolloEngordeService.cs` | `SqlQueryRaw<InformeSemanalRow>` + agrupado por semana + CONSOLIDADO. `CompanyId` del usuario autenticado. Array vía `NpgsqlParameter(Array|Integer)` |
| `API/Controllers/InformeSemanalPolloEngordeController.cs` | `POST api/InformeSemanalPolloEngorde/generar` `[Authorize]` |
| `Program.cs` | DI: `AddScoped<IInformeSemanalPolloEngordeService, InformeSemanalPolloEngordeService>()` |

**Endpoint:** `POST /api/InformeSemanalPolloEngorde/generar`
```jsonc
// body
{ "granjaIds": [37,41], "nucleoId": null, "galponId": null, "loteId": null,
  "fechaDesde": "2026-05-01", "fechaHasta": "2026-05-07" }
// respuesta: { filtrosAplicados, totalFilas, semanas: [ { semana, filas:[...], consolidado:{...} } ] }
```

---

## 7. Frontend (Angular 20 standalone)

Carpeta `frontend/src/app/features/informe-semanal-engorde/`:
- `services/informe-semanal-engorde.service.ts` — interfaces (contrato == DTO) + `generar()`.
- `pages/informe-semanal-engorde-list/` — filtros (granja multi/all/una con cascada núcleo→galpón→lote
  cuando hay una sola granja; rango de fechas), tablas por semana + CONSOLIDADO, export Excel.
- Ruta en `app.config.ts`: `path: 'informe-semanal-engorde'` (`loadComponent`), `canActivate: [authGuard]`.

Las columnas "Tabla" se muestran en gris como «—» (placeholder).

---

## 8. Menú

Migración `20260623070654_AddMenu_InformeSemanalPolloEngordePanama` (idempotente):
- Inserta en `menus`: label **"Informe Semanal Pollo Engorde"**, route `/informe-semanal-engorde`,
  key `informe_semanal_pollo_engorde`, **bajo el grupo "Reportes" (id 24)**.
- Hereda `role_menus` y `company_menus` de **"Liquidacion tecnica"** (`/indicador-ecuador`).
- En local quedó como menú **id 59** (roles 9/9, empresas 2/2).

---

## 9. Cómo correr / probar en LOCAL

> ⚠️ `appsettings.json` (base) apunta a **PROD RDS**. La conexión local correcta está en
> `appsettings.Development.json` (`Host=localhost;Port=5433;Database=sanmarinoapplocal`).
> **Todo comando EF debe usar `ASPNETCORE_ENVIRONMENT=Development`** o apuntará a prod.

```bash
# Crear/aplicar migraciones (desde backend/src/ZooSanMarino.API), config Release para no chocar
# con la API en ejecución (que bloquea bin/Debug):
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project ../ZooSanMarino.Infrastructure --startup-project . \
  --context ZooSanMarinoContext --configuration Release

# Probar la función directo:
SELECT * FROM fn_informe_semanal_pollo_engorde(3, NULL, NULL, NULL, NULL, '2026-05-01','2026-05-07');
```

---

## 10. Validación realizada

- ✅ Agregados crudos de la fn (mortalidad, selección, consumo semanal/acumulado, ventas, saldos)
  **coinciden exactamente** con la agregación manual sobre `fn_seguimiento_diario_engorde` para
  el lote 95 (Panamá) y la empresa 3 (8 granjas / 85 lotes).
- ✅ Filtros verificados: todas las granjas (NULL), array de granjas, galpón, lote, y overlap de fechas
  (una semana calendario devuelve la semana de vida correcta por lote).
- ✅ Columnas "Tabla" = NULL (placeholder). Consolidado: AVES = suma, tasas/pesos = promedio.
- ✅ `dotnet build -c Release` 0 errores · `yarn build` OK.
- ⏳ **Pendiente:** smoke test e2e del endpoint con JWT (requiere reiniciar la API con el nuevo build).

---

## 11. Dónde quedó el proceso (para continuar la integración)

1. **CONECTAR LA TABLA GENÉTICA** (ver §4): definir con el cliente "suma por semana vs valor del día 7",
   y reemplazar los `NULL::numeric AS *_tabla*` / `pct_*` en
   `backend/sql/fn_informe_semanal_pollo_engorde.sql` por el join a `guia_genetica_ecuador_detalle`
   (por raza/año del lote, `sexo`, `dia`/semana). Luego: nueva migración EF `UpdateFnInformeSemanal...`,
   y exponer las columnas en el front (ya existen en el DTO/interfaz, sólo dejarán de ser «—»).
2. **Confirmar denominador** de % mortalidad/selección (§5).
3. **VPI** (columna del Excel): es una referencia cruzada a otra semana/archivo; definir fuente real.
4. **Datos del Excel del cliente** (DOÑA MARIA, TROFARELLO) son de prod; no están en local. Para validar
   números exactos contra el Excel, correr la fn en un entorno con esos lotes.
5. **Smoke test e2e** del endpoint autenticado tras reiniciar la API.
