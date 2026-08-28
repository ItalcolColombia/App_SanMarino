# Índice único por DÍA en los seguimientos diarios (engorde, levante, reproductora)

> **Estado: BORRADOR VALIDADO, no aplicado.** La migración existe, compila, tiene su simulación en
> transacción corrida contra la copia de producción del 28-ago-2026 y sus tests. **Falta el OK
> explícito para aplicarla.**
>
> Diagnóstico reproducible: [`backend/sql/verificar_duplicados_dia_seguimiento.sql`](../backend/sql/verificar_duplicados_dia_seguimiento.sql)

---

## 1. El problema

Los índices únicos de seguimiento son sobre `(lote, fecha)` con `fecha` de tipo **`timestamptz`**, así
que comparan el **instante**, no el día. Y los escritores no usan la misma hora:

| Escritor | Hora que guarda | Filas (engorde) |
|---|---|---:|
| Formulario manual, convención vieja | `17:00Z` (mediodía **local**) | 4.745 |
| Formulario manual, convención actual | `12:00Z` (mediodía **UTC**) | 1.894 |
| **Trigger del cruce de reproductora** | `00:00Z` | 330 |

Las dos manuales caen en el mismo día calendario mirándolas desde cualquier zona. **La del cruce es la
única ambigua**, y es la que produce todas las colisiones reales: una fila del cruce y una manual del
mismo día conviven sin que el índice se entere.

### Lo que eso causa

El día sale **dos veces en la tabla diaria**, con mortalidad y consumo sumados. En el lote 161 el
saldo de alimento llega a **−3.461 kg (29-jun)** y **−8.406 kg (30-jun)**.

---

## 2. Alcance medido (copia de producción, 28-ago-2026)

| Tabla | Días duplicados | Filas sobrantes |
|---|---:|---:|
| engorde | 5 → **4** | 5 → **4** |
| levante | 1 | 1 |
| reproductora | **0** | 0 |
| producción | **0** | 0 |

Producción da 0 porque **ya tiene el índice funcional** desde
`20260801070000_IndiceUnicoSeguimientoProduccionDia`. Ese es el precedente que esta migración copia.

El 5.º de engorde (`12676`, lote 216 DAYLAND, 17-ago) **se borró por la UI** durante la validación:
era el único sin efecto aplicado. Los otros 4 quedan.

---

## 3. La decisión de diseño: por qué el índice es PARCIAL y no se borra nada

Las 4 filas históricas de engorde y las 2 de levante **ya aplicaron su efecto**: cada una tiene su
movimiento en `inventario_gestion_movimiento` y su fila en `lote_registro_historico_unificado`.

| id | lote | día | reservas | histórico | movimiento inventario |
|---|---|---|---:|---:|---:|
| 10859 | 161 | 28-jun | 0 | **1** | **1** |
| 10860 | 161 | 29-jun | 0 | **1** | **1** |
| 10861 | 161 | 30-jun | 0 | **1** | **1** |
| 11224 | 178 | 27-jul | 0 | **1** | **1** |
| 12676 | 216 | 17-ago | 2 activas | 0 | 0 | ← borrada por la UI |
| 1089 / 1090 | levante 127 | 11-jul | 0 | — | **1** c/u |

Borrarlas dejaría el movimiento huérfano y el histórico apuntando a un seguimiento inexistente. El
histórico unificado **se anula, nunca se abandona** — y anular a ciegas ya se demostró peor que no
tocar (las 93 filas huérfanas que habrían mandado 5 ciclos cerrados a saldo negativo).

**Por eso se excluyen por id del índice, con nombre y apellido, y se protege todo lo demás.** Un alta
futura sobre esos mismos días sí tendría id nuevo y sí entraría al índice, así que la exclusión no
abre un agujero permanente.

`12676` se excluye igual, aunque ya no exista en local: en producción todavía está, y así la migración
no depende de que alguien lo haya borrado antes. Si no existe, la exclusión queda inerte.

### Fail-soft, como el precedente

Si al correr quedan duplicados **fuera** de la lista de excluidos, el índice **no se crea** y queda un
`RAISE WARNING` en el log del deploy. Nunca se tira el arranque de producción por esto — es la lección
del incidente SIGSEGV de migraciones.

---

## 4. Qué se toca

| Archivo | Cambio |
|---|---|
| `Migrations/20260828120000_IndiceUnicoDiaSeguimientos.cs` | **+** 4 índices únicos funcionales por día UTC, parciales donde hace falta. |
| `Migrations/20260828120000_IndiceUnicoDiaSeguimientos.Designer.cs` | Clon del Designer anterior (ModelSnapshot **sin tocar**: EF no declara estos índices). |
| `Application/Calculos/DuplicadoSeguimientoDiarioCalculos.cs` | **+** traduce la violación de unicidad al mensaje del usuario, para los **dos** nombres de índice. |
| `API/Controllers/SeguimientoAvesEngordeController.cs` | Los 2 `catch` de duplicado delegan en el cálculo. |
| `tests/…/DuplicadoSeguimientoDiarioCalculosTests.cs` | **+** fija el contrato nombre-de-índice ↔ mensaje. |

### Por qué el cambio en el controller no es opcional

El controller reconocía el duplicado **por nombre de índice** (`uq_seg_diario_aves_engorde_lote_fecha`)
para dar un mensaje claro. El caso nuevo —manual chocando con una fila del cruce— dispara el índice
**nuevo**, cuyo nombre el controller no conocía: el usuario habría recibido el texto crudo de Postgres
justo en el caso que veníamos a proteger. El nombre viaja de una migración a un `if` sin que ningún
compilador los relacione, así que el test fija los dos.

---

## 5. Validación ya corrida

**Simulación en transacción** contra la copia de producción (`BEGIN … ROLLBACK`):

| # | Prueba | Resultado |
|---|---|---|
| 1 | Los 4 índices se crean | ✅ los 4, sin warnings ⇒ las listas de excluidos son correctas |
| 2 | Insertar el 22-ago a `03:00Z` (día ya ocupado a otra hora) | ✅ **rechazado** por `ux_seg_diario_aves_engorde_lote_dia_utc` — el caso exacto que el índice viejo dejaba pasar |
| 3 | Insertar un día libre (26-ago) | ✅ entra: no bloquea lo legítimo |
| 4 | Las 4 filas históricas excluidas | ✅ siguen conviviendo, historia intacta |
| 5 | Tras el `ROLLBACK` | ✅ 0 índices persistidos |

**Build y tests:** `dotnet build` 0 errores / 0 advertencias · `dotnet test` **3487/3487** (+8 nuevos).

---

## 6. Lo que este trabajo NO resuelve

**El origen sigue ahí: el cruce escribe a `00:00Z` y el formulario a `12:00Z`.** El índice impide que
la colisión se persista, pero la insinúa como un error en vez de evitarla.

Alinear el cruce a mediodía UTC parecía la corrección de fondo, y **no se puede hacer tal cual**: el
cruce borra sus filas y re-inserta **sin `ON CONFLICT`**, así que donde ya exista una fila manual de
ese día la confirmación de reproductora fallaría entera. Cambiaría un duplicado silencioso por un
error duro en producción. Alinearlo exige primero darle al cruce una estrategia de conflicto — otra
entrega, con su propio plan.

---

## 7. Antes de aplicar

1. Re-correr `backend/sql/verificar_duplicados_dia_seguimiento.sql` contra el dump del día: si aparece
   un duplicado nuevo fuera de la lista, la migración lo saltea con warning y hay que ampliar la lista.
2. Confirmar si `12676` sigue existiendo en producción y decidir si se borra por la UI antes (deja el
   histórico más limpio) o se deja excluido.
3. `dotnet build` + `dotnet test` en verde (ya está).
4. Aplicar por deploy, nunca `dotnet ef database update` contra RDS.
5. Post-deploy: verificar que los 4 índices existen y que **no** quedó ningún `RAISE WARNING` en el log.
