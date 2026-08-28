# Corregir las aves de un lote de engorde: el CHECK de la BD rechaza la auditoría

**Fecha:** 28-ago-2026 · **Módulo:** pollo engorde (`LoteAveEngorde`) · **Reporta:** usuario en producción
**Caso concreto:** granja **SACACHUN 3B**, galpón 3, **LOTE 04** — hay que **sumar 200 aves hembra**
(8.614 → 8.814 encasetadas; el saldo vivo 8.047 debe pasar a 8.247).

---

## 1. Síntoma y causa raíz

El formulario *Editar Lote de Engorde* devuelve el toast:

> **Error** — Alguno de los valores no cumple una regla de validación de la base de datos.

Ese texto tiene **un solo emisor** en todo el repo:
`ErrorPersistenciaCalculos.DescribirErrorSql` para **SQLSTATE `23514` = `check_violation`**.

El `UpdateAsync` del lote llama a `AplicarAjusteEncasetamientoAsync`
(`LoteAveEngordeService.cs:521`), que —desde el commit `a9fd721` (21-ago-2026)— deja la auditoría del
ajuste como una fila en `historial_lote_pollo_engorde` con:

```csharp
private const string TipoRegistroAjusteEncaset = "AjusteEncaset";   // LoteAveEngordeService.cs:35
```

y la tabla, en producción, tiene:

```sql
CONSTRAINT ck_hlpe_tipo_registro CHECK (tipo_registro IN ('Inicio', 'Ajuste', 'AjusteResync'))
```

**`AjusteEncaset` no está en la lista** ⇒ el `INSERT` de auditoría revienta con 23514 y **toda la
transacción se cae**: no se guarda el ajuste, ni el `Inicio` corregido, ni `aves_encasetadas`, ni el
saldo. El lote queda exactamente igual y el usuario ve el toast genérico.

El valor `AjusteResync` sí está porque lo agregó la migración
`20260611172121_CorreccionSaldosAvesEngorde2601y2602`. **Al `AjusteEncaset` nunca se le escribió la
suya**: la funcionalidad se mergeó con el `.cs` que lo escribe y sin la migración que lo permite. Es
literalmente la regla *«el `.sql` es el espejo, la migración es el vehículo»* de CLAUDE.md, cobrada.

### Por qué no se vio antes: la BD local NO tiene ningún CHECK

Medido hoy contra `sanmarinoapplocal` (:5433):

```
SELECT count(*) FROM pg_constraint WHERE contype='c' AND connamespace='public'::regnamespace;  →  0
```

**Cero** constraints CHECK en todo el esquema local — ni `ck_hlpe_*`, ni siquiera los dos que EF
declara en `LoteAveEngordeConfiguration` (`ck_lae_nonneg_counts`, `ck_lae_nonneg_pesos`). La copia
local se restauró sin ellos. Por eso el ajuste **funciona perfecto en local y sólo falla en producción**,
y por eso el arreglo del 21-ago se dio por bueno: la clase entera de bugs es invisible acá.

> Consecuencia operativa: **un `dotnet test` verde y un smoke local no prueban nada sobre los CHECK.**
> La verificación de esta entrega se hace recreando la constraint de producción en una transacción.

### Segundo defecto, del mismo origen: restar aves tampoco podría guardar

La fila de auditoría guarda el **delta**, que es **negativo** cuando el ajuste QUITA aves. Y la tabla
tiene además:

```sql
CONSTRAINT ck_hlpe_aves_nonneg CHECK (aves_hembras >= 0 AND aves_machos >= 0 AND aves_mixtas >= 0)
```

El caso de hoy es una **suma** (+200), así que sólo lo frena el primer CHECK. Pero apenas se arregle
ese, el caso simétrico —el operario que digitó **de más** y quiere bajar— chocaría con el segundo,
con el mismo toast inútil. Se arreglan **los dos en la misma migración**: es un solo defecto
(la auditoría del ajuste no cabe en el catálogo de la tabla) con dos caras.

---

## 2. Enfoque

**Nada de código nuevo en el camino de escritura.** El service ya hace lo correcto (base reemplazada,
saldo corrido por delta, las tres copias juntas). Lo que falta es que la BD acepte la fila de
auditoría que ese código ya escribe.

1. **Migración EF idempotente** que amplía el catálogo de `tipo_registro` a los **cuatro** valores que
   el código escribe y **relaja** `ck_hlpe_aves_nonneg` para que el delta de un `AjusteEncaset`
   pueda ser negativo — y sólo el de ese tipo.
2. **Catálogo puro en `Application/Calculos`** con esos cuatro valores + los predicados de negocio
   (participa en la conservación, admite delta negativo), consumido por los services, con **tests
   xUnit** que congelan la lista contra la que quedó en la migración. Si mañana alguien inventa un
   quinto `tipo_registro` en C# sin migración, **el test falla en el gate de CI** en vez de fallar en
   producción con un toast genérico.
3. **Espejo `.sql`** (`backend/sql/create_historial_lote_pollo_engorde.sql`) actualizado a lo mismo.

### Por qué la constraint se relaja y no se borra

`ck_hlpe_aves_nonneg` sigue protegiendo a `Inicio`, `Ajuste` y `AjusteResync`, que son cantidades y no
pueden ser negativas nunca. Sólo `AjusteEncaset` es un **delta con signo**. Borrarla entera dejaría
pasar un `Inicio` negativo, que es exactamente lo que la constraint existe para impedir.

### Por qué es seguro para los lectores

Los **seis** lectores de la tabla filtran `tipo_registro` de forma explícita:

| Lector | Filtro |
|---|---|
| `LoteAveEngordeService.ProjectToDetail` (inicialH/M/X) | `== 'Inicio'` |
| `MovimientoPolloEngordeService.ResumenDisponibilidad` (×2) | `== 'Inicio'` |
| `LiquidacionCongeladaAplicador` | `== 'Inicio'` |
| `CorreccionAvesDisponiblesEngordeService` | `== 'Inicio'` y `== 'Ajuste'` |
| `fn_cuadre_aves_engorde.sql` | `= 'Inicio'` y `= 'Ajuste'` |

Ninguno suma la tabla entera ⇒ una fila `AjusteEncaset` (positiva o negativa) es **inerte** para
todos. Es el mismo criterio con que se incorporó `AjusteResync` en junio.

---

## 3. Archivos

| Archivo | Cambio |
|---|---|
| `Migrations/20260828190000_AmpliaCheckHistorialEngordeAjusteEncaset.cs` | **nuevo** — DDL idempotente de las dos constraints |
| `Migrations/20260828190000_AmpliaCheckHistorialEngordeAjusteEncaset.Designer.cs` | **nuevo** — Designer clonado del anterior (el modelo EF no cambia) |
| `Application/Calculos/TipoRegistroHistorialEngordeCalculos.cs` | **nuevo** — catálogo puro |
| `Infrastructure/Services/LoteAveEngordeService.cs` | consts → catálogo (mismos literales) |
| `Infrastructure/Services/CorreccionAvesDisponiblesEngordeService.cs` | idem |
| `Infrastructure/Services/LoteReproductoraAveEngordeService.cs` | idem |
| `backend/sql/create_historial_lote_pollo_engorde.sql` | espejo actualizado |
| `tests/ZooSanMarino.Application.Tests/TipoRegistroHistorialEngordeCalculosTests.cs` | **nuevo** |

**No se toca** `ZooSanMarinoContextModelSnapshot.cs`: estas constraints no están en el modelo EF (la
configuración de `HistorialLotePolloEngorde` nunca las declaró), viven sólo en la BD.

---

## 4. SQL de la migración

- `ck_hlpe_tipo_registro`: `Inicio | Ajuste | AjusteResync | AjusteEncaset`. Se recrea sólo si
  **ninguna fila viola el catálogo nuevo** (imposible si la constraint ya existe, porque el catálogo
  nuevo es un superconjunto del viejo). Si hubiera basura, no se toca nada y queda un `RAISE WARNING`:
  jamás se tira el arranque de producción por esto (lección del incidente SIGSEGV).
- `ck_hlpe_aves_nonneg`: se reemplaza **sólo si ya existe** — relajar un predicado no puede fallar
  sobre datos existentes. Donde no exista, no se crea: esta entrega no inventa invariantes nuevos.

## 5. Reglas de negocio

- El **inicial** se reemplaza; el **saldo vivo** se corre por el delta (ya implementado — no cambia).
- Restar por debajo de lo ya consumido sigue rechazándose con el mensaje que nombra el día
  (`AjusteEncasetamientoCalculos.Diagnosticar`) — es un 400 explicado, no un 23514.
- `AjusteEncaset` **no participa en la conservación**: el ajuste ya está dentro del `Inicio`
  corregido. Igual que `AjusteResync`.
- El ajuste sigue exigiendo el permiso `lote.corregir_aves`
  (`CorreccionAvesLoteAutorizacionCalculos`) — no se relaja.

## 6. Casos de prueba

**xUnit (`TipoRegistroHistorialEngordeCalculosTests`):**
1. El catálogo es exactamente `{Inicio, Ajuste, AjusteResync, AjusteEncaset}` — congela la lista de la migración.
2. Cada literal que los services escriben hoy es válido según el catálogo.
3. `AdmiteDeltaNegativo` es `true` sólo para `AjusteEncaset` — espeja el CHECK relajado.
4. `ParticipaEnConservacion` es `true` sólo para `Ajuste`.
5. Comparación **case-sensitive**, y valor desconocido / vacío / nulo ⇒ inválido (fail-closed).

**Verificación contra Postgres (transacción + ROLLBACK), simulando producción:**
1. Crear en local `ck_hlpe_tipo_registro` con la lista VIEJA + `ck_hlpe_aves_nonneg`.
2. `INSERT` de un `AjusteEncaset` ⇒ debe fallar con **23514** (reproduce el bug del usuario).
3. Correr el SQL de la migración.
4. Repetir el `INSERT` ⇒ debe **pasar**. Y con delta **negativo** ⇒ también pasa.
5. `INSERT` de un `Inicio` con aves negativas ⇒ debe **seguir fallando** (la protección se conserva).
6. `INSERT` con `tipo_registro` inventado ⇒ debe **seguir fallando**.
7. Segunda corrida del SQL ⇒ **no-op** (idempotencia).
8. `ROLLBACK`.

**Fuera de alcance de esta entrega:** el deploy. La migración se aplica sola al arrancar la app
(`Database__RunMigrations=true`), pero el push/merge a `main-produccion` va con OK aparte.
