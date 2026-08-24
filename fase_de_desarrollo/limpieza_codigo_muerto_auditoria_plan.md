# Limpieza de código muerto detectado en la auditoría (23-ago-2026)

Dos piezas de código que no ejecuta nadie y que, si alguien las tocara, harían daño. Salieron de la
auditoría completa back+front del 23-ago-2026 (builds y tests en verde: back 0/0 y 3.135 tests,
front 0/0 y 633 tests).

## Enfoque arquitectónico

**Borrar, no arreglar.** Las dos son código que ya perdió su razón de existir. Arreglarlas sería
dejar viva una segunda implementación compitiendo con la real (caso 1) o una llave de seguridad
suelta que nadie usa pero cualquiera puede girar (caso 2).

Refactor ≠ cambio de comportamiento: ninguna de las dos participa en un flujo vivo, así que la app
debe comportarse **exactamente igual** después. La verificación es precisamente esa: mismos builds,
mismos tests, mismo número de tests.

---

## Caso 1 — `SeguimientoLoteLevanteFormComponent` (front)

**Qué es:** un formulario de seguimiento de levante en
`features/lote-levante/pages/seguimiento-lote-form/`.

**Por qué se va:**
1. **Nadie navega a él.** Está ruteado en `/daily-log/seguimiento/nuevo` y `/editar/:id`, pero
   ningún botón, link ni `router.navigate` del repo apunta ahí. El formulario **real** de levante es
   `ModalCreateEditComponent`, que abre `seguimiento-lote-levante-list`.
2. **Además está roto.** Declara `ChangeDetectionStrategy.OnPush` (línea 27) y después asigna
   `this.lotes` dentro de un `subscribe` de HTTP (línea 58) y `this.loading` en un `finalize`
   (línea 156). Las dos se renderizan en el template (`@for (l of lotes; ...)`, `@if (loading)`).
   Bajo OnPush esas asignaciones no ensucian la vista ⇒ el `<select>` de lotes saldría **vacío** y el
   overlay de guardado quedaría **colgado**. Es el bug canónico que documenta CLAUDE.md.

Un formulario huérfano y roto es una trampa: el día que alguien lo enlace "porque ya existe", hereda
el bug entero.

**Archivos:**
- borrar `pages/seguimiento-lote-form/` completa (`.ts`, `.html`, `.scss`)
- `seguimiento-lote-levante-routing.module.ts`: quitar el import y las rutas `nuevo` y `editar/:id`
- `seguimiento-lote-levante.module.ts`: quitar el import y la entrada de `imports`

La ruta `''` → `SeguimientoLoteLevanteListComponent` **se conserva**: es la que sí se usa.

---

## Caso 2 — `AllowAllPolicyProvider` (back, `Program.cs`)

**Qué es:** un `IAuthorizationPolicyProvider` que devuelve `RequireAssertion(_ => true)` para la
policy default, la fallback y **cualquier policy con nombre**.

**Por qué se va:** hoy **no está registrado** en DI, así que es inerte — la autorización real la
define `AddAuthorization` con `FallbackPolicy = RequireAuthenticatedUser` (línea 531). Pero sigue
compilando dentro del ensamblado: **una sola línea** (`AddSingleton<IAuthorizationPolicyProvider,
AllowAllPolicyProvider>()`) apaga la autorización de los 93 controllers a la vez, en silencio y sin
romper ningún test. El comentario de la línea 523 ya dice que el allow-all "antes existía"; lo que
faltó fue borrar la clase.

**Archivos:**
- `ZooSanMarino.API/Program.cs`: eliminar la clase y su encabezado (líneas 977-1000, cola del archivo)
- `ZooSanMarino.Application/Interfaces/IDbStudioAuthorization.cs`: el doc-comment afirma que las
  policies de ASP.NET "están neutralizadas por el AllowAllPolicyProvider" — **eso ya es falso** y
  desinforma sobre seguridad. Se corrige el texto (sin tocar la interfaz).

Los comentarios históricos de `Program.cs` (523 y 536) **se conservan**: narran por qué el deny-by-
default existe y esa historia sigue siendo cierta y útil.

## Cambios de BD/SQL

Ninguno. No hay migración, ni SQL, ni contrato de API involucrado.

## Casos de prueba

Al ser eliminación de código inalcanzable, la prueba es de **no-regresión**, no de comportamiento nuevo:

| # | Verificación | Esperado |
|---|---|---|
| 1 | `dotnet build` | 0 errores, 0 warnings (igual que antes) |
| 2 | `dotnet test` | 3.135 pasan, 0 fallan (mismo número que antes) |
| 3 | `yarn build` | 0 errores, 0 warnings |
| 4 | `yarn test` | 633 pasan (mismo número que antes) |
| 5 | grep de referencias residuales | 0 menciones a `SeguimientoLoteLevanteFormComponent` y a `AllowAllPolicyProvider` fuera de los comentarios históricos |
| 6 | Autorización sigue cerrada | `FallbackPolicy = RequireAuthenticatedUser` intacto; los 4 `[AllowAnonymous]` de `AuthController` siguen siendo los únicos públicos |

Si el conteo de tests cambia, algo se llevó por delante código vivo y hay que revertir.
