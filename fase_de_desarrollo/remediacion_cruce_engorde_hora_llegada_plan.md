# Remediar las filas de cruce ya torcidas por la hora de llegada (engorde, ItalcolPanamá)

> Continuación de `hora_llegada_manda_primer_dia_engorde_plan.md` (commit `151cebe`). Aquel arregló al
> **escritor**: `fn_cruce_reproductora_a_engorde` ya no fecha en el día del encaset cuando las aves
> llegaron ≥ 13:00. **No recalculó nada**, y lo dijo: las filas viejas quedaron para «una operación de
> datos aparte, con su propia verificación y su propio OK». Este plan es esa operación.

## 1. Alcance real medido (copia de producción, 28-ago-2026)

⚠️ **Todas las mediciones van con la sesión en `UTC`.** `fecha` es `timestamptz` guardada a `00:00Z`;
con el `TimeZone` por defecto de la máquina (`America/Bogotá`) `fecha::date` **resta un día** y muestra
6 violaciones donde hay 3. Es la trampa de [[plazo-validacion-vencia-a-las-19]] otra vez.

**3 filas violan la regla; las 3 son `origen_cruce` / `SYSTEM_CRUCE`, las 3 de ItalcolPanamá:**

| Lote | Nombre | Galpón | Encaset | Hora | Fila | Consumo | Mort+Sel | Estado del lote |
|---|---|---|---|---|---|---|---|---|
| 215 | 14 - 1 | G0471 | 10-ago | 23:30 | id 12118 · 10-ago | 362,880 kg | 46 | **vivo** |
| 216 | 14 - 2 | G0471 | 13-ago | 22:40 | id 12168 · 13-ago | 181,440 kg | 16 | **vivo** |
| 238 | PRUEBA - 1 | G0475 | 27-ago | 23:58 | id 12937 · 27-ago | 150,000 kg | 5 | **borrado** (28-ago 14:17) |

- **215 y 216 comparten el galpón G0471** ⇒ comparten el stock de alimento: su saldo diario es el mismo
  número. Lo que se le haga a uno mueve el cuadre del otro.
- **238 es el lote del ticket y está borrado.** No queda nada que arreglarle al usuario que reclamó;
  lo que queda vivo son 215 y 216.
- **Ningún lote de Ecuador entra**: sus 19 lotes con hora ≥ 13:00 no usan el cruce (0 filas
  `origen_cruce`) y no tienen una sola violación.

### 1.1 Los lotes vivos NO tienen saldo negativo

El síntoma del ticket (**−150 kg**) era del lote 238, borrado. Medido sobre 215 y 216:
**0 días con saldo de alimento negativo**, mínimo **+1.558,08 kg**. Lo que queda es la fila que no
debería existir, no un saldo en rojo.

### 1.2 Un segundo defecto, independiente: el encaset del reproductora se movió

`lote_reproductora_ave_engorde` **131** (hijo del 215) tiene `fecha_encasetamiento` = **09-ago**,
un día antes que su padre (10-ago), y fue **editado el 25-ago 12:52** — cuatro días *después* de que
corriera el cruce (21-ago 16:57). Nadie volvió a correr el cruce.

Como el cruce **mapea por EDAD**, ese desfase corre la serie entera otra vez: hoy las filas del 215
están donde las dejó el encaset viejo, no donde las pondría la fn con el dato de hoy.

**No es un problema general:** 128 de 138 lotes reproductora están alineados (delta 0); los 10
desalineados incluyen deltas de 7, 18 y 29 días, que son otra historia y **no** entran acá.

## 2. El hallazgo que decide el mecanismo

> 🔴 **Una migración SQL a secas rompe un invariante del proyecto.**

El descuento de aves al maestro **no lo hace el SQL**: lo hace C#
(`RetiroAvesEngordeAplicador.SincronizarCruceAsync`), que además escribe la fila `BAJA_SEGUIMIENTO` en
`lote_registro_historico_unificado`. Verificado en la copia: el histórico del 215 tiene **id 17821 →
origen_id 12118, `anulado = false`**, viva y apuntando justo a la fila que habría que sacar.

Si una migración borra o re-fecha las filas de cruce en SQL:
- el histórico queda **huérfano y sin anular** — exactamente lo que CLAUDE.md prohíbe
  («El histórico unificado se ANULA, nunca se abandona… el saldo seguiría contándola»);
- el maestro `hembras_l/machos_l` conserva descontadas **62 aves** (46 del 215 + 16 del 216) cuyo
  seguimiento ya no existe.

Replicar esa lógica en SQL duplicaría la fórmula (**«una sola fórmula por número»**). ⇒ **La
remediación tiene que pasar por el camino C#**, no por una migración de datos.

## 3. Las opciones, con los números medidos (transacción revertida)

Cuadre del galpón **G0471** (`fn_cuadre_alimento_engorde`), saldo de alimento y saldo de aves:

| Opción | 215 filas/kg | 216 filas/kg | Saldo alim. G0471 | **Descuadre G0471** | Aves 215 | Aves 216 | Viola |
|---|---|---|---|---|---|---|---|
| **0 · hoy** | 7 / 5.080,320 | 7 / 1.542,240 | 4.189,85 | **−634,64** | 30.232 | 9.866 | 2 |
| **1 · recalcular** | 5 / 2.993,760 | 6 / 1.360,800 | 6.457,85 | **+1.633,36** | 30.316 (+84) | 9.876 (+10) | 0 |
| **2 · borrar sólo el día del encaset** | 6 / 4.717,440 | 6 / 1.360,800 | 4.734,17 | **−90,32** | 30.278 (+46) | 9.882 (+16) | 0 |
| **3 · alinear encaset + recalcular** | 6 / 3.991,680 | 6 / 1.360,800 | 5.459,93 | **+635,44** | 30.277 (+45) | 9.876 (+10) | 0 |

Las tres dejan **0 violaciones** y **ninguna toca una fila manual** (verificado: 0 borradas, 0 nuevas,
todas idénticas). El **cuadre de aves** (`fn_cuadre_aves_engorde`) queda en **desfase 0 / `cuadra = t`**
en las tres.

### 3.1 Por qué el recálculo pierde kilos

Al correr la serie un día, **el último día del cruce cae sobre un registro manual** que ya ocupa esa
fecha, y la fn lo saltea con `ON CONFLICT DO NOTHING` + `RAISE WARNING` (el guarda que agregó el commit
anterior — sin él, esto **reventaba** con `duplicate key`). Medido:

```
WARNING: lote 215, edad 6: el dia 2026-08-17 ya estaba ocupado por otro registro; el cruce se omitio.
WARNING: lote 215, edad 7: el dia 2026-08-18 ya estaba ocupado por otro registro; el cruce se omitio.
WARNING: lote 216, edad 6: el dia 2026-08-20 ya estaba ocupado por otro registro; el cruce se omitio.
```

El 215 pierde **dos** días (997,920 + 1.088,640 = **2.086,560 kg**) porque además arrastra el desfase de
encaset de §1.2; el 216 pierde **uno** (181,440 kg). Total **2.268,00 kg** que dejan de contarse
⇒ el cuadre del galpón salta de −634,64 a **+1.633,36**: sobra alimento en la tabla que en el galpón no
está.

### 3.2 Lo que el stock físico dice

La opción **2** es la única que deja el cuadre **casi en cero (−90,32 kg)**: el stock del galpón es
consistente con «los 12 días de cruce menos el día del encaset». Es evidencia física a favor de que
esas dos filas sobran y de que **los demás días son reales**.

### 3.3 Pero la 2 no es estable

La 2 deja los datos donde **la fn no los pondría**. El trigger `trg_cruce_reproductora_engorde` corre
`AFTER INSERT OR UPDATE OR DELETE` sobre el seguimiento reproductora: **el primer toque a la
reproductora convierte la 2 en la 1**, con sus 2.268 kg. Los únicos estados estables son la **1** y la
**3**.

## 4. Casos de prueba (los mismos, sea cual sea la opción)

1. Cero filas con `fecha < encaset + desplazamiento` en los lotes 215 y 216.
2. Ninguna fila **manual** movida, borrada ni creada (comparación por `id`).
3. `lote_registro_historico_unificado`: **cero** filas `BAJA_SEGUIMIENTO` no anuladas apuntando a un
   `origen_id` inexistente, en TODO el universo (no sólo en estos lotes).
4. `fn_cuadre_aves_engorde`: desfase 0 y `cuadra = true` en 215 y 216.
5. `fn_cuadre_alimento_engorde`: el descuadre de G0471 termina donde dice la tabla de §3, sin sorpresas.
6. **No regresión multipaís**: los otros 64 galpones del cuadre, sin una fila de diferencia.

## 5. Decisión tomada — **opción 3** (alinear encaset + recalcular)

Elegida con los números a la vista. Es la única **coherente con la regla ya desplegada** («se corre la
serie, no se descarta el día: el consumo del reproductora es real»), la única **estable** que además
corrige un dato maestro genuinamente torcido, y pierde **la mitad** de los kilos que el recálculo a
secas. El lote **238 se deja como está** (borrado el 28-ago 14:17); la cohorte lo excluye sola por
`deleted_at IS NULL`.

## 6. Implementación

**Migración `20260828200000_RemediarCruceEngordeHoraLlegadaPanama`** (data-only, Designer clonado,
ModelSnapshot intacto). SQL en el partial `.Sql.cs`, para que el archivo principal se pueda leer.

**La cohorte se resuelve por la REGLA, no por ids fijos**: lote engorde vivo, con
`hora_encasetamiento >= 13:00`, `aves_encasetadas > 0` y al menos una fila `origen_cruce` anterior a
`encaset + 1`. En la copia de producción eso da exactamente **215 y 216**. Si prod trajera otro, entra
solo; si no trajera ninguno, la migración no hace nada.

| Paso | Qué hace | Por qué |
|---|---|---|
| 0 | Congela la cohorte y **tres respaldos**: maestro previo + encaset del reproductora, las filas de cruce completas, y los ids del histórico que se anulan/insertan | Sin ellos el `Down` no puede distinguir lo que movió esta migración de lo que ya estaba |
| 1 | Devuelve las aves al maestro y **anula** el histórico de las filas que van a morir | Paso 1 de `SincronizarCruceAsync`, corrido **antes** del borrado, que es cuando todavía se lee el baseline |
| 2 | Alinea `lote_reproductora_ave_engorde.fecha_encasetamiento` con la del padre | §1.2 — sin esto el 215 se mueve dos días y pierde 2.086,560 kg en vez de 1.088,640 |
| 3 | Llama `fn_cruce_reproductora_a_engorde` por lote | La fn es **la única fórmula del número**. El `UPDATE` del paso 2 no dispara el trigger: vive sobre el *seguimiento* reproductora, no sobre el lote |
| 4 | Aplica al maestro las bajas de las filas **nuevas** y escribe su histórico | Paso 2 del aplicador, con su mismo reparto (`EsLoteMixto`), su **clamp a 0** y la guarda `aves_encasetadas > 0` |

**De UNA sola vez, no convergente.** Todo el `Up` va dentro de un bloque guardado por la tabla de
respaldo. Si se re-corriera, el paso 3 volvería a borrar y recrear las filas de cruce con ids
**nuevos** y el histórico de la corrida anterior quedaría huérfano y **sin anular** — justo el
invariante que esto viene a cuidar.

## 7. Validación — corrida, no asumida

Todo medido contra la copia de producción **con el SQL extraído del `.cs` que se despacha** (verificado
byte a byte contra el borrador ensayado), en una transacción con `ROLLBACK`.

| Verificación | Resultado |
|---|---|
| `dotnet build` | **0 errores / 0 advertencias** (con `--artifacts-path` aislado: había 2 `dotnet` de otras sesiones) |
| `dotnet test` | **3.519/3.519** en verde (3.518 Application + 1 Domain) |
| `dotnet ef migrations list` | la reconoce `(Pending)` y **última** en el orden |
| Gate `verificar-sql-llega-por-migracion.js` | OK |
| **Up** · lote 215 | 7 filas / 5.080,320 kg / 10-ago..16-ago → **6 / 3.991,680 / 11-ago..16-ago** |
| **Up** · lote 216 | 7 filas / 1.542,240 kg / 13-ago..19-ago → **6 / 1.360,800 / 14-ago..19-ago** |
| **Up** · violaciones | **2 → 0** |
| **Up** · descuadre G0471 | **−634,64 → +635,44 kg** |
| **Up** · maestro | 215: 15.175/15.087 → **15.196/15.111** · 216: 4.944/4.954 → **4.949/4.959** |
| **Up** · cuadre de aves | desfase **0/0**, `cuadra = true` en ambos |
| **Up** · filas manuales | **byte a byte idénticas** (16 filas, mismo id y misma fecha) |
| **Up** · otros 64 galpones | **misma huella** `md5 = e926003d…` — 0 regresión multipaís |
| **Up** · huérfanas del universo | **6 → 6** (las preexistentes del lote 227; no se agrega ninguna) |
| **Segunda corrida** | `NOTICE: remediacion_cruce_hora: ya aplicada, no se repite` — **no mueve un solo número** |
| **Down** | vuelve al estado inicial **línea por línea**: 7 filas / 5.080,320 kg, maestro 15.175/15.087, encaset repro 09-ago, descuadre −634,64, violaciones 2 |
| BD local | **intacta** — todo corrió en transacción revertida; 0 tablas de respaldo, las 2 violaciones siguen ahí |

## 8. Lo que se pierde, dicho de frente

**1.088,640 kg** del 215 y **181,440 kg** del 216. Al correr la serie, el último día del cruce cae sobre
un **registro manual** que ya ocupa esa fecha y la fn lo saltea con `ON CONFLICT DO NOTHING` +
`RAISE WARNING` (el guarda que agregó `151cebe`; sin él esto **reventaba** con `duplicate key`). No hay
salida sin pérdida: las dos fuentes reclaman el mismo día.

## 9. Fuera de alcance

- **No se despliega.** La migración queda commiteada; el deploy es otra entrega con su OK.
- **No se aplica en la BD local a propósito**: `dotnet ef database update` arrastraría también las tres
  migraciones pendientes de otras sesiones, y la BD local es una sola para todos los checkouts.
- **Lote 227**: 6 filas `BAJA_SEGUIMIENTO` huérfanas y sin anular (142 aves), creadas el 28-ago 06:03.
  Mismo invariante roto por **otro camino**; no lo toca esta tarea.
