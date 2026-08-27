# DB Studio — el backup ordena funciones y vistas por separado (27-ago-2026)

## Síntoma

Restaurar `sanmarino-2026-08-27-produccion (1).sql` en la local corta en la línea 138089:

```
ERROR:  relation "vw_guia_genetica_postura" does not exist
LINE 138089:               FROM vw_guia_genetica_postura gg
SQL state: 42P01
```

## Diagnóstico

`DbStudioService.Backup.cs` emite **dos bloques separados y en este orden**: primero
`WriteRoutinesAsync` (funciones, ya en orden topológico desde el 13-ago) y después
`WriteViewsAsync` (vistas, **alfabéticas**). El encabezado del archivo lo dice textual:
*«Funciones en orden topológico …, vistas alfabéticas al final»*.

Esa separación asume que las funciones nunca leen vistas y que las vistas nunca se leen entre
sí. **Las dos cosas son falsas hoy** (medido sobre el dump del 27-ago):

| Quién | Usa | Bloque emisor |
|---|---|---|
| `fn_indicadores_levante_postura` | `vw_guia_genetica_postura` | función → vista |
| `fn_indicadores_produccion_postura` | `vw_guia_genetica_postura` | función → vista |
| `fn_resumen_semanal_ra_pesadas_levante` | `vw_guia_genetica_postura` | función → vista (**la que revienta**) |
| `fn_resumen_semanal_ra_pesadas_produccion` | `vw_guia_genetica_postura` | función → vista |
| `vw_guia_genetica_por_lote_postura` | `vw_guia_genetica_postura` | vista → vista |
| `vw_guia_genetica_por_lote_postura` | `f_safe_numeric` | vista → función |

Dos fallas, no una:

1. **Función → vista.** Las 4 funciones son `LANGUAGE sql`: su cuerpo se valida contra el
   catálogo AL CREARSE. Como las vistas van 1.676 líneas más abajo, revientan con 42P01. Es el
   mismo mecanismo del incidente del 13-ago (42883 con funciones), con otra clase de objeto.
2. **Vista → vista (latente, todavía no vista porque `ON_ERROR_STOP` corta antes).** El bloque
   de vistas es alfabético y `vw_guia_genetica_por_lote_postura` **precede** a
   `vw_guia_genetica_postura` (`por_` < `pos`), pero la lee. Al arreglar (1) sin arreglar (2),
   el restore volvería a cortar 1.676 líneas después.

**Por qué no alcanza «vistas antes que funciones»**: `vw_guia_genetica_por_lote_postura` llama a
`f_safe_numeric`, o sea que la dependencia va en los dos sentidos. El orden correcto es **uno
solo, topológico, sobre funciones y vistas juntas**.

## Enfoque

### 1. Un único bloque ordenado (Application/Calculos — lógica pura)

`DbStudioSqlCalculos` generaliza lo que ya tiene:

- `ObjetoEsquemaDef(long Orden, string Name, string Definition, TipoObjetoEsquema Tipo)` —
  `Tipo` ∈ {`Funcion`, `Vista`}. `Orden` es la clave de desempate: OID para funciones (orden de
  creación, como hoy), posición alfabética para vistas.
- `OrdenarObjetosEsquemaPorDependencia(...)` — Kahn sobre el grafo mixto. La arista se detecta
  **según el tipo del objeto referido**:
  - hacia una **función** → `RutinaInvocaA` (exige `(` a la derecha; descarta prefijos), sin cambios;
  - hacia una **vista** → `DefinicionUsaRelacion` (frontera de palabra a los dos lados, **sin**
    paréntesis: una vista se nombra `FROM vw_x g`, no `vw_x(...)`).
- Desempate `(Tipo, Orden)`: entre varios objetos listos sale primero una función y, dentro de
  cada tipo, el de menor `Orden`. Así la salida queda lo más cerca posible del archivo de hoy
  (todas las funciones, después las vistas) y solo se adelanta lo que una dependencia obliga.
- `OrdenarRutinasPorDependencia` queda como envoltorio (todo `Funcion` ⇒ desempate por OID puro):
  los 9 tests del 13-ago siguen siendo el contrato y no se tocan.
- Ciclo por arista falsa ⇒ degradación al orden previo `(Tipo, Orden)`; la salida es siempre una
  permutación exacta de la entrada.

### 2. Emisión (Infrastructure/…/DbStudioService.Backup.cs)

- `WriteRoutinesAsync` + `WriteViewsAsync` → **un solo** `WriteRutinasYVistasAsync`: junta rutinas
  (`pg_get_functiondef` por OID) y vistas (`GetViewsAsync` + `GetViewDefinitionAsync`, que
  conservan el filtro de autorización), ordena y emite cada objeto con su sintaxis
  (`CREATE OR REPLACE VIEW` / `DROP MATERIALIZED VIEW` + `CREATE MATERIALIZED VIEW`).
- Se conserva el best-effort por vista: la que no se pueda exportar sale como comentario
  `-- [omitida]` y no aborta el backup.
- **Red de seguridad `SET check_function_bodies = off;`** antes del bloque (es lo que hace
  `pg_dump`): si alguna vez se escapa una arista que el texto no delata (SQL dinámico en un
  `EXECUTE`, una función sin argumentos invocada sin paréntesis), la función se crea igual y
  queda sana apenas exista el objeto. Las **vistas se siguen validando** —el GUC no las toca—,
  así que el orden topológico sigue siendo obligatorio, no decorativo.
- Encabezado del archivo y marcador de sección actualizados (el consejo de re-correr «el tramo
  entre X y `-- Triggers`» tiene que nombrar el bloque nuevo, que ahora incluye las vistas).

### 3. El dump ya descargado

No se puede regenerar (sale de producción). Se reordena el archivo con un script de un solo uso
que aplica **la misma regla** y se restaura en la local con `-v ON_ERROR_STOP=1`: eso valida el
algoritmo contra el catálogo real, no solo contra los tests.

## Casos de prueba (xUnit, `DbStudioSqlCalculosTests`)

1. Función que lee una vista ⇒ la vista sale antes (el caso que reventó).
2. Vista que lee otra vista, declarada al revés del alfabeto ⇒ orden correcto.
3. Vista que llama a una función ⇒ la función sale antes.
4. Cadena mixta función → vista → función ⇒ orden completo.
5. Sin dependencias ⇒ funciones primero (por OID) y vistas después (alfabéticas): el archivo de
   hoy no se reordena porque sí.
6. `DefinicionUsaRelacion`: fronteras de palabra, calificado con esquema, entrecomillado,
   prefijo/sufijo pegado (no matchea).
7. Permutación exacta de la entrada con ciclo mixto (nada se pierde ni se duplica).
8. Los 9 tests de rutinas del 13-ago siguen verdes (no-regresión del envoltorio).

## Validación

- `dotnet build` 0 errores / 0 advertencias nuevas + `dotnet test` completo.
- Restore real del dump reordenado sobre `sanmarinoapplocal` **vacía**, con
  `-v ON_ERROR_STOP=1`: 0 errores, y conteo de tablas/filas contra el pie del archivo.
- Verificación en la base restaurada de que las 59 funciones y las 5 vistas existen.
