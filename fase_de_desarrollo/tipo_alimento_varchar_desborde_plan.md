# Plan — `tipo_alimento` desborda `varchar(100)` y tumba el guardado del seguimiento diario

**Fecha:** 2026-08-06
**Incidente:** «Falla al guardar» en el lote **A374A** (Agroavicola Sanmarino, LA ESMERALDA) — el toast
muestra `An error occurred while saving the entity changes. See the inner exception for details.` y al
reabrir la pantalla no hay nada guardado.

---

## 1. Diagnóstico (reproducido en local, BD restaurada de producción)

Excepción interna real: **`Npgsql.PostgresException 22001: value too long for type character varying(100)`**
al insertar en `seguimiento_diario_levante`.

`modal-create-edit.component.ts → onSave()` arma `tipoAlimento` **concatenando los nombres** de los
alimentos elegidos:

```
"H: <nombre>" / "M: <nombre>" / "G: <nombre>"   unidos por " / "
longitud = Σ(len(nombre)) + 6n − 3
```

`seguimiento_diario_levante.tipo_alimento` es **`varchar(100)`** y la pantalla **no limita** cuántas
filas de alimento se agregan (`agregarItemHembras` / `agregarItemMachos` / `agregarItemGeneral`).
Los alimentos de reproductora de la empresa 1 miden 30–35 caracteres:

| Alimentos | Largo | Resultado |
|---|---|---|
| 2 (1 H + 1 M) | 75–79 | guarda |
| 3 (2 H + 1 M) | 113–120 | **22001 → 500** |

**Confirmación en datos:** el `length(tipo_alimento)` máximo de TODA la tabla es **79**. Nunca entró un
registro con tres alimentos, en ningún lote. A374A venía usando exactamente 79 ⇒ 21 caracteres de margen.

**No es el lote.** Afecta a cualquier lote de Levante de cualquier empresa donde el operario agregue un
tercer alimento. A374A aparece señalado solo porque ahí lo están haciendo.

**Por qué no queda nada a medias:** en Colombia el alta va dentro de una transacción atómica
(seguimiento + descuento de inventario) ⇒ rollback completo. Verificado: 0 filas nuevas, aves del lote
intactas, stock intacto. **No hay datos corruptos: es un fallo puro de escritura.**

**Por qué el mensaje es ilegible:** `Program.cs:602` devuelve `ex?.Message` tal cual, que para un
`DbUpdateException` es el texto genérico de EF. El `SqlState` real nunca sale del servidor.

**Origen de la deuda:** el feature «hasta 2 alimentos por género» se hizo **front-only sin migración**.
La tabla hermana `seguimiento_diario_lote_reproductora_aves_engorde.tipo_alimento` **ya está en
`varchar(500)`** (alguien pegó contra lo mismo y la ensanchó solo para ese módulo) y la carga masiva lo
parchó **truncando a 100** (`MigracionService.Historicos.ResolverTipoAlimento`, commit `fd6e51f`).
Levante y engorde quedaron sin arreglar.

---

## 2. Enfoque arquitectónico

`tipo_alimento` es una cadena de **PRESENTACIÓN** (tabla diaria + Excel). El dato real por ítem vive en
`metadata.itemsHembras / itemsMachos / itemsGenerales` y es el que alimenta consumo, inventario y
cálculos. Ampliar la columna **no toca ninguna aritmética ni reporte**.

Tres capas, ninguna cambia el comportamiento de los caminos que hoy funcionan:

1. **BD** — ampliar la columna a `varchar(500)` (mismo tamaño que la tabla hermana ya corregida).
2. **Backend** — red de seguridad: recortar a la longitud de la columna ANTES de persistir, para que un
   catálogo con nombres largos nunca vuelva a tumbar un guardado entero. Lógica pura en
   `Application/Calculos` con tests.
3. **API** — traducir el `SqlState` de Postgres a un mensaje accionable en vez del texto de EF.
   Estrictamente aditivo: si el `SqlState` no está mapeado, se conserva el mensaje actual.

**Descartado:** limitar la cantidad de alimentos en el front (mutila una función legítima — el operario
cambia de fase de alimento y necesita cargar dos) y truncar sin ampliar (recorta texto que el usuario ve).

---

## 3. Archivos a crear / modificar

### Nuevos
| Archivo | Qué |
|---|---|
| `Application/Calculos/TipoAlimentoCalculos.cs` | `MaxLongitud = 500` + `Recortar(valor, max)`. Puro. |
| `Application/Calculos/ErrorPersistenciaCalculos.cs` | `DescribirErrorSql(sqlState, mensajeCrudo)` → mensaje en español o `null`. Puro. |
| `Infrastructure/Migrations/<ts>_AmpliarTipoAlimentoSeguimientos.cs` | DDL idempotente. |
| `tests/…/TipoAlimentoCalculosTests.cs` | Recorte, borde exacto, null/vacío, idempotencia. |
| `tests/…/ErrorPersistenciaCalculosTests.cs` | 22001/23505/23503/23502 + desconocido → null. |

### Modificados
| Archivo | Cambio |
|---|---|
| `Configurations/SeguimientoDiarioConfiguration.cs:31` | `TipoAlimento` `HasMaxLength(100)` → `(500)` |
| `Configurations/SeguimientoDiarioAvesEngordeConfiguration.cs:27` | ídem |
| `Configurations/SeguimientoDiarioAvesEngordeEcuadorConfiguration.cs:26` | ídem |
| `SeguimientoLoteLevanteService.Mapeos.cs` (2 puntos) | `TipoAlimento: TipoAlimentoCalculos.Recortar(dto.TipoAlimento)` |
| `SeguimientoAvesEngordeService.Crud.cs` (2 puntos) | ídem |
| `SeguimientoAvesEngordeEcuadorService.Crud.cs` (2 puntos) | ídem |
| `MigracionService.Historicos.cs:605,624` | `MaxTipoAlimento` local → delega en `TipoAlimentoCalculos` |
| `Program.cs:600-602` | `DbUpdateException` con inner `DbException` → `DescribirErrorSql` |

**Fuera de alcance (a propósito):**
- `seguimiento_diario_levante.tipo_alimento_hembras/_machos` (varchar 100): **nadie las escribe** — ningún
  cliente envía `tipoAlimentoHembrasNombre` y en BD están 100 % en NULL. Guardan UN nombre (máx. 35), no
  una concatenación. Ampliarlas sería ruido.
- `lote_seguimientos` (tabla deprecada) y `plan_gramaje_galpon.tipo_alimento` (clave de búsqueda de
  gramaje, no una concatenación).
- `seguimiento_diario_produccion.tipo_alimento` ya es `text` ⇒ producción nunca estuvo afectada.

---

## 4. Cambios de BD / SQL

Migración `20260806063157_AmpliarTipoAlimentoSeguimientos`, **idempotente** (CLAUDE.md): un `DO $$` que
solo hace el `ALTER` si la columna **existe**, su largo actual es **menor** a 500 y **no hay vistas
colgadas de ella**.

```
seguimiento_diario_levante.tipo_alimento    varchar(100) → varchar(500)
```

### ⚠️ Alcance recortado durante la validación local — engorde queda FUERA

La primera versión abarcaba también las tres tablas de engorde. **Al aplicarla en local falló**:

```
0A000: cannot alter type of a column used by a view or rule
Where: ALTER TABLE public.seguimiento_diario_aves_engorde ALTER COLUMN tipo_alimento TYPE character varying(500)
```

La vista de Power BI **`vw_seguimiento_pollo_engorde`** depende de esa columna. Ampliarla exigiría
dropear y recrear la vista dentro de una migración que **se aplica sola en cada deploy**, con riesgo de
perder sus permisos sin que nadie lo note. Engorde no es el módulo del incidente, así que:

- `seguimiento_diario_aves_engorde` y `_ecuador` se quedan en **varchar(100)** (configurations incluidas)
  y quedan cubiertas por el recorte de `TipoAlimentoCalculos.MaxLongitudEngorde`: el texto se acorta,
  pero el guardado **no se cae**.
- La migración regenerada por EF tras ese ajuste ya solo toca levante — y de paso deja de arrastrar
  `seguimiento_diario_aves_engorde_ecuador`, que está **mapeada en el modelo pero no existe** en la base
  (la creó `20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry` y luego desapareció): correr ese
  `AlterColumn` en prod habría matado el arranque de la app.

El `Up()` conserva una **guarda de vistas dependientes** que omite el ALTER con `WARNING` en vez de
fallar, por si algún entorno tuviera una vista sobre `seguimiento_diario_levante` que local no tiene.
Un deploy que no aplica el ancho es recuperable; uno que no arranca, no (§🚀).

**Verificar post-deploy** (si la guarda se disparara, el ancho no se aplicó):
```sql
select character_maximum_length from information_schema.columns
where table_schema='public' and table_name='seguimiento_diario_levante' and column_name='tipo_alimento';  -- esperado: 500
```

**Ampliar un `varchar` en Postgres no reescribe la tabla** (desde 9.2): es un cambio de catálogo,
instantáneo, sin lock largo ni riesgo sobre los datos existentes. `Down()` inverso e igual de idempotente,
pero **sin achicar por debajo del dato más largo** (aborta con mensaje si hay filas que no entrarían).

---

## 5. Reglas de negocio

- **R1** — `tipo_alimento` es presentación; el cálculo sale de `metadata`. Ampliar/recortar no altera
  consumo, inventario, saldo ni indicadores.
- **R2** — El recorte es **red de seguridad**, no la ruta feliz: con la columna en 500 y nombres de ≤35,
  harían falta ~14 alimentos en un día para llegar al tope.
- **R3** — El recorte conserva el **prefijo** (`texto[..max]`), igual que el que ya existe en la carga
  masiva. Sin puntos suspensivos, para no inventar caracteres que no eligió el usuario.
- **R4** — `DescribirErrorSql` devuelve `null` ante un `SqlState` no mapeado ⇒ el handler global cae al
  mensaje de hoy. Cero regresión en los errores que ya se muestran bien.

---

## 6. Casos de prueba

### xUnit — `TipoAlimentoCalculos`
| # | Entrada | Esperado |
|---|---|---|
| T1 | `null` | `null` |
| T2 | `""` | `""` |
| T3 | 499 chars | intacto |
| T4 | exactamente 500 | intacto (borde inclusivo) |
| T5 | 501 chars | 500 chars, prefijo exacto |
| T6 | recortar dos veces | idempotente |
| T7 | max explícito 100 (contrato viejo de la carga masiva) | 100 chars |
| T8 | el string real de 3 alimentos de A374A (113) | intacto con max=500 |

### xUnit — `ErrorPersistenciaCalculos`
| # | SqlState | Esperado |
|---|---|---|
| E1 | `22001` | mensaje de «texto demasiado largo» |
| E2 | `23505` | mensaje de duplicado |
| E3 | `23503` | mensaje de referencia inexistente |
| E4 | `23502` | mensaje de dato obligatorio |
| E5 | `42P01` / `null` / vacío | `null` (cae al mensaje actual) |

### Smoke end-to-end local (backend real + BD restaurada de prod)
| # | Caso | Esperado |
|---|---|---|
| S1 | 3 alimentos (2 H + 1 M, 113 chars) en lote 116 A374A | **201** y `tipo_alimento` completo (113) — hoy 500 |
| S2 | 2 alimentos (1 H + 1 M, 76 chars) — control de no regresión | 201, idéntico a hoy |
| S3 | Descuento de inventario y de aves del caso S1 | exacto, sin doble descuento |
| S4 | Editar (PUT) el registro de S1 cambiando alimentos | 200, sin 22001 |
| S5 | Eliminar los registros del smoke | devolución exacta de stock y aves |
| S6 | `tipoAlimento` de 600 chars (por encima del nuevo tope) | 201 con recorte a 500, **no** 500 HTTP |
| S7 | Estado final de la BD | idéntico al snapshot previo |

### Validación de build
- `cd backend && dotnet build` → 0 errores, sin advertencias nuevas
- `cd backend && dotnet test` → verde (1622 previos + los nuevos)
- `dotnet ef database update` local + `has-pending-model-changes` → «No changes»
- Segunda pasada de la migración → no hace nada (idempotencia probada)

---

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| La migración corre en prod contra una tabla inexistente → SIGSEGV en el arranque | `IF EXISTS` por tabla y por columna |
| `ModelSnapshot` desalineado (acá el modelo SÍ cambia, a diferencia de `AlinearLoteIdInventarioAves`) | dejar el snapshot que genera EF y verificar con `has-pending-model-changes` |
| BD local compartida entre worktrees | ampliar es compatible hacia atrás: una rama vieja sigue escribiendo ≤100 sin problema |
| Recorte silencioso oculta un catálogo mal cargado | se registra `LogWarning` cuando el recorte efectivamente corta |
