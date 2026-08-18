# El nombre de empresa activa deja de ser una cabecera en la que se confía

**Origen:** hallazgo V23.3 del bloque *«V23 · B10»*, que apareció al smokear la revocación por dato.
**Fecha:** 2026-08-18 · Bloque propio — no tocar desde otras sesiones.

---

## 1. El defecto, en una línea

`HttpCurrentUser.cs:22`:

```csharp
// SIEMPRE leer el header X-Active-Company, independientemente de la autenticación
ActiveCompanyName = http?.Request.Headers["X-Active-Company"].FirstOrDefault();
```

`ActiveCompanyMiddleware` **sí** valida la pertenencia y publica el resultado en
`HttpContext.Items` (`EffectiveCompanyId` / `EffectiveCompanyName`). `ICurrentUser.CompanyId` lee ese
resultado validado. **`ActiveCompanyName` no**: devuelve el header crudo, tal como llegó.

Y 44 archivos hacen exactamente esto:

```csharp
private async Task<int> GetEffectiveCompanyIdAsync()
{
    if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
    {
        var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
        if (byName.HasValue) return byName.Value;   // ← empresa elegida por el cliente
    }
    return _current.CompanyId;                       // ← la validada, que nunca se llega a usar
}
```

⇒ **cambiando una cabecera, un usuario lee con el alcance de una empresa que no es la suya.** La
empresa validada por el middleware queda de adorno: el `return` de abajo casi nunca se alcanza.

## 2. Que no es teórico — el caso que se va a demostrar

`item_inventario_ecuador` tiene datos en 5 empresas (Sanmarino 61 · **ItalcolEcuador 152** · Demo 62 ·
ItalcolPanama 148 · Santa Reyes 45) y `ItemInventarioService` filtra **sólo** por la empresa resuelta.
Un usuario que pertenece **únicamente** a Sanmarino, mandando `X-Active-Company: ItalcolEcuador`,
debería ver 61 ítems y va a ver 152.

> Por qué el smoke de V23 no filtró nada: se probó contra `LoteService.GetAllAsync`, que **además**
> corta por las granjas asignadas al usuario (fail-closed) ⇒ la intersección daba vacío. Esa segunda
> guarda existe en algunos servicios y en otros no. La que falta es la primera.

## 3. El arreglo

**Una propiedad, no 44 archivos.** `ActiveCompanyName` pasa a devolver **el nombre que validó el
middleware** (`EffectiveCompanyName` de `HttpContext.Items`), nunca el header. Con eso:

- Si el usuario pertenece a la empresa pedida (o es super admin) ⇒ el middleware la aprobó, el nombre
  está, y los 44 servicios resuelven **la misma** empresa que hoy. **Sin cambio de comportamiento.**
- Si NO pertenece ⇒ el middleware no publica nada ⇒ `ActiveCompanyName` es `null` ⇒ los 44 servicios
  caen a `_current.CompanyId`, que es la empresa **de su token**. **Fail-closed.**

La regla se escribe una vez, pura y testeada, en `Application/Calculos/EmpresaActivaCalculos.cs`, y la
usan el middleware (para decidir) y `HttpCurrentUser` (para exponer).

### 3.1 Segunda pata: que el nombre no pueda volver a divergir del id

`CompanyResolver.GetCompanyIdByNameAsync` hace `ILike(name)` + `FirstOrDefaultAsync()` **sin orden** y
`companies.name` **no tiene índice único** (verificado: 2 índices, ninguno sobre `name`; hoy hay 0
nombres duplicados). Con dos empresas homónimas, el id resuelto sería no determinista.

Ya que el middleware conoce el **id exacto** que aprobó, el resolver devuelve ese id cuando el nombre
pedido es el de la empresa activa validada. Deja de haber una segunda fuente de verdad en el camino
caliente, y de paso se ahorra una consulta por llamada.

## 4. Casos de prueba

**Cálculo puro (xUnit):**
- T1 middleware aprueba (miembro) ⇒ nombre confiable = el validado.
- T2 middleware NO aprueba ⇒ nombre confiable = `null` (no el header).
- T3 super admin sobre empresa ajena ⇒ aprueba.
- T4 sin sesión ⇒ no se inventa empresa (fail-closed).
- T5 el nombre se normaliza (trim) y `""`/espacios ⇒ `null`.

**Smoke antes/después (el mismo, corrido dos veces):**
- **T6 — la fuga, reproducida.** Usuario sólo de Sanmarino + `X-Active-Company: ItalcolEcuador`
  ⇒ **hoy** debe listar los **152** ítems de Ecuador.
- **T7 — cerrada.** El mismo pedido, después del arreglo ⇒ **61**, los suyos.
- **T8 — sin regresión.** El mismo usuario pidiendo **su** empresa ⇒ 61 antes y después.
- **T9 — el super admin no pierde nada**: pidiendo ItalcolEcuador sigue viendo los 152.

## 5. Cambios de BD / SQL

**Ninguno.** No se agrega el índice único sobre `companies.name`: no se puede verificar hoy si
producción tiene homónimos y un `CREATE UNIQUE INDEX` que falla deja el deploy a medias. Queda
señalado; la no-determinación ya la cubre la pata 3.1.

## 6. Fuera de alcance

- **No** se reescriben los 44 `GetEffectiveCompanyIdAsync` duplicados. Ahora son correctos porque su
  entrada es confiable; unificarlos es limpieza, no seguridad, y merece su propio paso.
- **No** se toca `X-Active-Pais`, que se lee igual de crudo pero **no** decide alcance de empresa.

---

## 7. Resultado (18-ago-2026)

### La fuga, medida antes y después — misma petición, mismo usuario

| Caso | Antes | Después |
|---|---:|---:|
| **T6/T7** usuario **sólo de Sanmarino** manda `X-Active-Company: ItalcolEcuador` | 🔴 **152 ítems de Ecuador** | ✅ **61**, los suyos |
| **T8** el mismo usuario pide **su** empresa | 61 | ✅ **61** (sin regresión) |
| **T9** el **super admin** pide ItalcolEcuador | 152 | ✅ **152** (no pierde alcance) |

La fuga se **reprodujo primero contra el código de HEAD**, para no arreglar algo supuesto: el usuario
`prueba@sanmarino.com.co`, que pertenece únicamente a Sanmarino, recibió la lista completa de ítems de
inventario de ItalcolEcuador con sólo cambiar una cabecera.

### Lo demás

- `dotnet build` **0 errores** (9 advertencias preexistentes).
- `dotnet test` **2.826 + 1 en verde** (+17: los T1-T12 del cálculo puro).
- Pantallas del caso normal sin cambios (`config/lotes`, `config/farms-list`, `config/item-inventario`)
  y pestaña limpia con **0 errores de consola**.
- BD compartida **sin una sola escritura**; puertos 5002/4200 libres.

### Alcance del arreglo

**Una propiedad y una regla pura.** Los 44 servicios que resuelven su empresa por nombre no se
tocaron: pasan a ser correctos porque su entrada ya es confiable. El middleware, además, usa ahora la
**misma** regla pura en sus dos ramas (por id y por nombre), que antes estaban escritas por separado.
