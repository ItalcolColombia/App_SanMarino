# El cruce de reproductora crea los días de pollo engorde SIN validar, y eso traba el lote

> **Origen:** reporte de Panamá (25-ago-2026): «en las reproductoras se confirmaron tarde, entonces
> en pollo engorde está bloqueado hacer el seguimiento porque no ha cambiado el estado de
> confirmación tarde; no deja crear otro seguimiento diario».
>
> **Base de medición:** copia de producción en `sanmarinoapplocal:5433`. Todo número está medido.

---

## 1. El defecto, en una línea

`fn_cruce_reproductora_a_engorde` inserta los días 1-7 de pollo engorde **sin escribir la columna
`validado`** (que tiene `DEFAULT false`), mientras el código C# documenta explícitamente lo
contrario. Si la reproductora se confirmó tarde, esos registros **nacen ya vencidos** y bloquean el
lote — y nadie puede destrabarlo, porque son de solo lectura en la UI.

### El contrato que se rompe

`backend/src/ZooSanMarino.Domain/Entities/SeguimientoDiarioAvesEngorde.cs`, propiedad `Validado`:

> *«Los registros con `OrigenCruce` **nacen validados**: los escribe el trigger de BD desde
> reproductora, ya confirmados en su origen, y nadie los edita a mano.»*

Verificado contra la función **desplegada** en la BD:

```
seguimiento_diario_aves_engorde.validado   →  DEFAULT false, NOT NULL
fn_cruce_reproductora_a_engorde            →  no menciona 'validado' ni una vez (7.563 chars)
```

El `INSERT` del cruce (`backend/sql/fn_cruce_reproductora_a_engorde.sql:153-186`) termina en
`metadata, origen_cruce, created_by_user_id, created_at` → `true, 'SYSTEM_CRUCE', now()`.
La columna `validado` sencillamente no está.

### Por qué explota solo cuando se confirma tarde

`ValidacionSeguimientoCalculos.DiasPlazoValidacion = 1`. El estado se deriva de la **fecha del
seguimiento**, no de cuándo se creó la fila:

```
Estado = validado ? VALIDADO : (hoy > fecha + 1 ? EN_RETRASO : PENDIENTE)
BloqueaAltaPorVencidos(flag, vencidos > 0)  ⇒  el lote no acepta días nuevos
```

Si la reproductora confirma **el mismo día**, el cruce nace con fecha de ayer/hoy y hay tiempo de
validar. Si confirma **tarde**, el cruce nace con fechas de hace días: **vencido en el instante en
que se crea**. Nunca hubo ventana.

### El caso reportado, medido

Lote **215** (DAYLAND · núcleo A · galpón «6» / `G0471` · `14 - 1` · ERP `G-4001014`, encaset
10-ago = 15 días — es la pantalla de la captura):

| | |
|---|---|
| Reproductora (lote 35) | 7 días confirmados **con 5 a 10 días de atraso** (19 y 21-ago para fechas del 10 al 16) |
| Pollo engorde | 7 registros con fechas **09 a 15-ago**, todos **creados el 21-ago** |
| Estado | los 7 en `validado = false`, `origen_cruce = true` |
| Reservas de alimento | **0 filas** — el cruce no crea ninguna |

Los 7 nacieron entre **6 y 12 días vencidos**.

---

## 2. Alcance

**Solo ItalcolPanama tiene `requiere_validacion_seguimiento_diario = true`** (las otras 4 empresas lo
tienen apagado, y ahí nadie lee `validado`). El defecto ha producido exactamente:

| | Registros | Lotes |
|---|---:|---|
| `origen_cruce` sin validar | **28** | 215, 216, 224, 225 — todos DAYLAND |

Dos de esos lotes (**224 y 225**) se crearon **hoy**: el problema está activo, no es histórico.

> Los 273 registros `origen_cruce` restantes de Panamá están en `validado = true` — los dejó así el
> backfill de la migración que estrenó la doble validación. O sea: **el backfill arregló el pasado y
> nadie arregló el futuro.**

---

## 3. El arreglo

1. **`fn_cruce_reproductora_a_engorde`**: el `INSERT` escribe
   `validado = true, validado_at = now(), validado_por = 'SYSTEM_CRUCE'`.
   Espejo en `backend/sql/` + **migración** (el `.sql` no llega solo a producción).
2. **Backfill** de los 28 existentes, acotado a `origen_cruce AND NOT validado`.
3. **Script de verificación** del invariante: ningún registro `origen_cruce` puede quedar sin validar.

### Por qué `validado = true` y no «excluir el cruce del conteo de vencidos»

- **Es el contrato que el C# ya documenta.** No se está inventando una regla: se está cumpliendo la
  que estaba escrita.
- **No hay nada que validar.** El cruce no crea reservas (`seguimiento_reserva_alimento`: 0 filas
  para esos registros), así que «validar» sería un no-op: no hay alimento separado que aplicar.
- **La confirmación humana ya ocurrió**, en reproductora (`confirmado`). Pedir una segunda sobre
  datos que el usuario **no puede editar** no comprueba nada.
- **Dejarlos pendientes es un estado sin salida**: son de solo lectura en la UI.
- **No duplica ningún descuento**: `RetiroAvesEngordeAplicador.SincronizarCruceAsync` descuenta las
  aves del cruce mirando `OrigenCruce` y el histórico, **nunca `validado`**.

Como el cruce regenera con `DELETE` + `INSERT`, el arreglo se aplica solo en cada regeneración.

---

## 4. Verificación

Ensayo en transacción revertida (`UPDATE ... WHERE origen_cruce AND NOT validado`, 28 filas):

| Lote | Galpón | Vencidos antes | De cruce | Propios | Vencidos después |
|---|---|---:|---:|---:|---:|
| 215 | 6 | 7 | 7 | 0 | **0** |
| 216 | 6 | 7 | 7 | 0 | **0** |
| 224 | 5 | 7 | 7 | 0 | **0** |
| 225 | 4 | 7 | 7 | 0 | **0** |
| **177** | 2 | 1 | 0 | **1** | **1** ⚠️ |

### ⚠️ El lote 177 NO lo arregla este fix, y está bien

Su registro vencido (id `12056`, fecha 20-ago) es **normal**, no de cruce: `origen_cruce = false`.
Los días vecinos sí están validados; ese quedó pendiente. Es **trabajo del operario, no un defecto**:
se destraba apretando **Validar** en la pantalla, porque a diferencia de los del cruce, ese registro
sí es editable.

Nombrarlo importa para que nadie lea «sigue habiendo un lote bloqueado» como que el arreglo falló.

---

## 5. Lo que este arreglo NO hace

- **No cambia el plazo de 1 día.** Que sea corto es una decisión del usuario («los registros tienen
  que ser validados con un día de diferencia como máximo»), y no es la causa: el problema es que el
  cruce nace del lado equivocado de la ventana, no que la ventana sea chica.
- **No toca reproductora.** Confirmar tarde es legítimo y hoy funciona.
- **No toca las otras 4 empresas**: con el flag apagado nadie lee `validado`. El backfill igual las
  alcanza para dejar el dato coherente si algún día lo encienden.
