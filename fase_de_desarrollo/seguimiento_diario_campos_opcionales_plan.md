# Plan — Campos opcionales en Seguimiento Diario (Levante y Producción), flag por empresa

## Requerimiento (tal como lo dio el usuario, 15-sep-2026)

> El registro de alimento en Levante y Producción, en los dos seguimientos, no sea obligatorio tenerlo,
> ya que pueden realizar varios seguimientos durante el día — entonces no siempre le dan alimento u otro
> campo. Se controla con un flag de empresa. Lo que no quedó en el ticket original: permitir registrar
> huevo, alimento o aves sin que sea obligatorio digitarlos todos — dependiendo del estado del lote, se
> puede mover solo mortalidad, solo consumo, o solo producción.

Alcance: **Levante** (`SeguimientoLoteLevanteService` / entidad `SeguimientoDiario` con
`TipoSeguimiento='levante'`) y **Producción** (`ProduccionService` / entidad `SeguimientoProduccion`).
Quedan **fuera**: Engorde (2 variantes) y Reproductora, aunque Levante comparte DTO/request/validador
con ellos — ver §"Blindaje de alcance".

---

## Enfoque arquitectónico

- Flag nuevo en `companies`: **`permite_seguimiento_diario_parcial`** (bool, `NOT NULL DEFAULT false`),
  nombrado por el comportamiento (patrón `permite_multiples_seguimientos_diarios`,
  `permite_traslado_aves_cross_etapa`), sembrado en `true` solo para Santa Reyes.
- Tres bloques de datos pasan a opcionales cuando el flag está ON: **alimento/consumo**, **aves**
  (mortalidad + selección/salida + error de sexaje) y **producción de huevos**. Con el flag OFF el
  comportamiento hoy vigente queda intacto en los dos módulos.
- **Regla de negocio confirmada por el usuario (15-sep-2026):** con el flag ON se permite guardar un
  seguimiento **completamente vacío**, sin datos en ningún bloque — no hay validador de mínimo, ni en
  front ni en back.
- **Relación con `permite_multiples_seguimientos_diarios` (flag ya existente): quedan ORTOGONALES, sin
  acoplarse en código.** Santa Reyes ya lo tiene activo (migración `20260912140000_SeedFlagMultiplesSeguimientosSantaReyes`),
  así que el caso de uso real (varios seguimientos parciales el mismo día) funciona out-of-the-box. Se
  documenta como prerrequisito **operativo** al activar el flag nuevo para otra empresa: sin
  `permite_multiples_seguimientos_diarios`, el alta de un segundo registro el mismo día se sigue
  rechazando aunque cada uno sea parcial. No se acopla en código porque un solo seguimiento diario
  parcial (p. ej. la empresa hace un único registro al día pero ese día no hubo producción reportable)
  es un caso válido independiente.
- **Hallazgo clave de la investigación (reduce el riesgo):** las funciones SQL canónicas
  `fn_seguimiento_diario_levante.sql` y `fn_seguimiento_diario_produccion.sql`, y sus espejos puros
  `SeguimientoDiarioLevanteCalculos`/`SeguimientoDiarioProduccionCalculos`, ya fueron reescritas en
  sep-2026 para tolerar varios registros/día (`SUM`, promedios solo sobre filas que "midieron",
  último-registro-gana para uniformidad/CV) — **no requieren cambios**. El caché `aves_*_actual` ya es
  null-safe (`?? 0`) y en Producción es explícitamente derivado/recalculado, no fuente. El trabajo real
  se concentra en **remover los gates de "obligatorio"**, no en la agregación.
- **Backend — dos mecanismos de "alimento obligatorio" distintos, hay que tocar los dos:**
  1. `[Required] string TipoAlimento` en `CrearSeguimientoRequest.cs:34` (Producción) — DataAnnotations,
     lo evalúa `[ApiController]` ANTES de que la action corra, **siempre**, sin importar ningún flag. Es
     el único bloqueador duro real de todo el contrato de Producción (mortalidad/huevos ya aceptan 0 por
     `Range`).
  2. `AlimentoObligatorioCalculos.Motivo` vía `SeparacionSeguimientoHelper.ValidarAlimentoObligatorio` —
     exige KILOS capturados en el bloque correcto, pero **solo corre bajo doble-validación**
     (`requiere_validacion_seguimiento_diario=true`, gate `if (separa)`). Es compartido por los 5 módulos
     de seguimiento (Levante, Producción, Engorde ×2, Reproductora).
  - Levante **no tiene** el mecanismo (1): su DTO/request son planos sin `[Required]`, y el único
    validador FluentValidation (`SeguimientoLoteLevanteDtoValidator`) es código muerto — nunca se ejecuta
    porque el controller bindea `CreateSeguimientoLoteLevanteRequest`, no el DTO que valida. **No se
    toca** (no es parte de este cambio; es una limpieza aparte, fuera de alcance).
- **Frontend:** replicar el patrón ya usado en `movimientos-pollo-engorde` para el flag
  `ventaEngordePesoDiferido` (`aplicarValidadoresPeso()` en `modal-venta-panama.component.ts:286-300` /
  `modal-movimiento-pollo-engorde.component.ts`): resolver el flag por `ActiveCompanyConfigService` en
  `ngOnInit`, guardar el booleano en un campo de instancia, y en un método dedicado
  `setValidators(...)`/`clearValidators()` + `updateValueAndValidity({ emitEvent: false })` según
  corresponda. **Fail-closed**: si el flag no resuelve (error del observable), se preserva el
  comportamiento restrictivo actual (todo obligatorio).

### Blindaje de alcance (para no tocar Engorde/Reproductora sin querer)

`ValidarAlimentoObligatorio`/`AlimentoObligatorioCalculos.Motivo` es compartido por los 5 módulos. El
parámetro nuevo (`permiteAlimentoOpcional: bool = false`) se agrega con default `false` (preserva
comportamiento en los 6 call sites que no se tocan) y **se pasa en `true` (resuelto desde el flag de
empresa) ÚNICAMENTE en los 4 call sites de Levante y Producción**:

- `SeguimientoLoteLevanteService.Crud.cs:38` (Create) y `:204` (Update)
- `ProduccionService.Seguimiento.cs:265` (Create) y `:688` (Update)

Los 6 call sites de `SeguimientoDiarioEngordeService.Crud.cs`, `SeguimientoAvesEngordeService.Crud.cs` y
`SeguimientoDiarioLoteReproductoraService.cs` **no se modifican** (quedan con el default `false`).

---

## Archivos a crear/modificar

### Backend

1. **Migración EF nueva** (`dotnet ef migrations add AddPermiteSeguimientoDiarioParcial --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext`, desde `/backend/src/ZooSanMarino.API/`):
   - `Up()`: `ALTER TABLE public.companies ADD COLUMN IF NOT EXISTS permite_seguimiento_diario_parcial boolean NOT NULL DEFAULT false;`
   - Seed dirigido: `UPDATE public.companies SET permite_seguimiento_diario_parcial = true WHERE name = 'Santa Reyes' AND permite_seguimiento_diario_parcial IS DISTINCT FROM true;` — **ordenada (timestamp) después** de la migración que crea/siembra Santa Reyes.
   - `Down()`: `ALTER TABLE public.companies DROP COLUMN IF EXISTS permite_seguimiento_diario_parcial;`
2. `backend/src/ZooSanMarino.Domain/Entities/Company.cs` — nueva propiedad `bool PermiteSeguimientoDiarioParcial { get; set; }` (doc XML corta, mismo estilo que `ClasificacionHuevoPorItems:42`).
3. `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs` — `.Property(x => x.PermiteSeguimientoDiarioParcial).HasColumnName("permite_seguimiento_diario_parcial").HasDefaultValue(false);` (junto a `:39-41`).
4. `backend/src/ZooSanMarino.Application/DTOs/CompanyDto.cs` (+ `CreateCompanyDto.cs`/`UpdateCompanyDto.cs` en la misma carpeta) — campo nuevo, `bool?` en el Update.
5. Las 4 proyecciones (mismo patrón que `ClasificacionHuevoPorItems`):
   - `CompanyService.cs` (`ToDto`, ~línea 46).
   - `CompanyService.Crud.cs` (alta ~línea 75, edición ~línea 150).
   - `CompanyResolver.cs` (2 proyecciones, ~líneas 71 y 136).
   - `CompanyPaisService.cs` (~línea 117).
6. `backend/src/ZooSanMarino.Application/Calculos/AlimentoObligatorioCalculos.cs`:
   - `Motivo(string modulo, bool loteEsMixto, AlimentoCapturado alimento, DateOnly? fecha, bool permiteAlimentoOpcional = false)` — si `permiteAlimentoOpcional` es `true`, devuelve `null` (cumple) sin evaluar `KgQueCuentan`.
   - `Cumple(...)` — agrega el mismo parámetro y lo reenvía a `Motivo`.
7. `backend/src/ZooSanMarino.Infrastructure/Services/ValidacionSeguimiento/SeparacionSeguimientoHelper.cs:35-48` —
   `ValidarAlimentoObligatorio(...)` agrega el parámetro `bool permiteAlimentoOpcional = false` y lo
   reenvía a `AlimentoObligatorioCalculos.Motivo`.
8. `SeguimientoLoteLevanteService.Crud.cs:38,204` y `ProduccionService.Seguimiento.cs:265,688` — resolver
   el flag de la empresa efectiva (mismo patrón `ResolverCompanyIdDeGranjaAsync` ya usado en el módulo) y
   pasarlo como `permiteAlimentoOpcional` en la llamada a `ValidarAlimentoObligatorio`.
9. `backend/src/ZooSanMarino.Application/DTOs/Produccion/CrearSeguimientoRequest.cs:34` — quitar
   `[Required]` de `TipoAlimento` (queda como `string TipoAlimento = string.Empty` o `string?`, a
   decidir según qué tan invasivo sea el nullable en el resto del archivo — preferible mantenerlo
   `string` no-nulo con default vacío para no tocar tipos aguas abajo).
10. `backend/src/ZooSanMarino.Infrastructure/Services/Funciones/ProduccionService.Seguimiento.cs` — al
    inicio de `CrearSeguimientoAsync`/`ActualizarSeguimientoAsync`, agregar el chequeo runtime que
    reemplaza al `[Required]` quitado: `if (!permiteAlimentoOpcional && string.IsNullOrWhiteSpace(request.TipoAlimento)) return BadRequest(...)`
    (mismo mensaje/forma que el 400 que armaba `Program.cs:740-759` para ModelState, o uno equivalente
    igual de claro). **Este chequeo corre siempre** (no solo bajo doble-validación) para no aflojar el
    comportamiento de las empresas sin el flag nuevo.
11. Tests xUnit — extender `backend/tests/ZooSanMarino.Application.Tests/AlimentoObligatorioCalculosTests.cs`
    con la matriz de `permiteAlimentoOpcional` (ver §Casos de prueba). Si se agrega un test de
    integración para el `TipoAlimento` de Producción, va en el test de servicio/controller existente de
    Producción (ubicar el archivo correspondiente al tocar el punto 10).

### Frontend

1. `frontend/src/app/core/services/company-config/active-company-config.service.ts`:
   - `interface CompanyFlags` — agregar `permiteSeguimientoDiarioParcial: boolean;` con doc-comment.
   - `const FLAGS_APAGADOS` — agregar `permiteSeguimientoDiarioParcial: false`.
   - `interface CompanyFlagsResponse` — agregar `permiteSeguimientoDiarioParcial?: boolean | null;`.
   - `mapFlags(dto)` — agregar `permiteSeguimientoDiarioParcial: dto?.permiteSeguimientoDiarioParcial === true`.
   - Método sugar `permiteSeguimientoDiarioParcial(): Observable<boolean>` (mismo patrón que
     `ventaEngordePesoDiferido()`).
2. `frontend/src/app/core/services/company/company.service.ts` — campo nuevo en `interface Company`
   (junto a `capturaHuevosEnLevante?: boolean;` ~línea 44).
3. `frontend/src/app/features/config/company-management/funciones/flags-empresa.funcion.ts` — entrada
   nueva en `FLAGS_EMPRESA` (grupo `'Postura'`, junto a `capturaHuevosEnLevante`).
4. `frontend/src/app/features/lote-levante/pages/modal-create-edit/modal-create-edit.component.ts`:
   - Campo de instancia `seguimientoParcial = false;` + método `aplicarValidadoresSeguimientoParcial()`
     llamado desde `ngOnInit`/apertura del modal (mismo lugar donde hoy se resuelven otros flags).
   - Con flag ON: `mortalidadHembras`/`mortalidadMachos`/`selH`/`selM`/`errorSexajeHembras`/`errorSexajeMachos`
     (líneas 566-571) pasan de `[Validators.required, Validators.min(0)]` a `[Validators.min(0)]`.
   - Dejar de forzar la fila de hembras obligatoria cuando el `FormArray` está vacío (757-759, 1394-1396)
     si el flag está ON.
   - Los 10 sitios de filas de ítems de alimento (1247-1250 … 1383-1386): cuando la fila está
     efectivamente vacía (sin `catalogItemId`) y el flag está ON, sus controles no bloquean el guardado
     — replicar el patrón `esFijo` opcional que ya usa Producción (`crearItemGroup`, ver punto 5).
   - Sin validador de mínimo: con el flag ON, el form puede quedar `valid` con todo en 0/vacío.
5. `frontend/src/app/features/lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts`:
   - Mismo patrón: `seguimientoParcial` + `aplicarValidadoresSeguimientoParcial()`.
   - `mortalidadH`/`mortalidadM`/`selH`/`selM` (329-332) pierden `required` con el flag ON.
   - `tipoAlimento` (365) y `pesoHuevo` (366) pierden `required` con el flag ON (`huevosTotales`/`huevosIncubables`/`etapa`
     ya están `.disable()`d — sin efecto en `form.invalid`, no requieren cambio).
   - Ítems no-fijos de alimento (459-462, 1125-1164): con el flag ON, tratarlos igual que `esFijo` (opcionales).
   - Sin validador de mínimo (mismo criterio que Levante).

---

## Cambios de BD/SQL

Una sola columna nueva en `companies` (ver punto 1 de Backend). **No** se tocan
`seguimiento_diario_levante`, `seguimiento_diario_produccion`, ni `fn_seguimiento_diario_levante.sql` /
`fn_seguimiento_diario_produccion.sql` — ya toleran filas parciales (confirmado en la investigación).

---

## Reglas de negocio

1. Flag OFF (default, todas las empresas salvo Santa Reyes): comportamiento actual preservado **byte a
   byte** en Levante y Producción — mismos mensajes de error, mismos 400.
2. Flag ON: se puede guardar un seguimiento diario con datos en un solo bloque (alimento, aves, o
   huevos); los demás quedan en 0/vacío. Aplica tanto al alta como a la edición.
3. **Se permite guardar un seguimiento completamente vacío** (sin datos en ningún bloque) — confirmado
   por el usuario el 15-sep-2026. No hay validador de mínimo ni en front ni en back.
4. La `Etapa` de Producción sigue siendo siempre obligatoria — es la clasificación estructural del
   registro, no un "dato" que el usuario decida omitir.
5. El flag no depende en código de `permite_multiples_seguimientos_diarios`; para el caso de uso
   completo (varios seguimientos parciales el mismo día) la empresa necesita ambos activos. Santa Reyes
   ya los tiene.
6. Engorde y Reproductora quedan fuera de este cambio — su exigencia de alimento no se toca.

---

## Casos de prueba

### Backend (xUnit, `AlimentoObligatorioCalculosTests.cs`)

Matriz sobre `Motivo`/`Cumple` con el nuevo parámetro `permiteAlimentoOpcional`:

| # | Módulo | `permiteAlimentoOpcional` | Alimento capturado | Resultado esperado |
|---|---|---|---|---|
| 1 | Levante/Producción | `false` | 0 kg | Rechaza (mensaje actual, sin cambios — regresión) |
| 2 | Levante/Producción | `false` | > 0 kg | Cumple (sin cambios — regresión) |
| 3 | Levante/Producción | `true` | 0 kg | **Cumple** (comportamiento nuevo) |
| 4 | Levante/Producción | `true` | > 0 kg | Cumple (sin cambios) |
| 5 | Engorde/Reproductora | (parámetro no pasado / default `false`) | 0 kg | Rechaza — confirma que el blindaje de alcance no se rompió |

### Backend (integración/servicio, Producción)

- `TipoAlimento` vacío/null + flag OFF ⇒ 400 (igual que hoy).
- `TipoAlimento` vacío/null + flag ON ⇒ 201/200.
- Doble-validación ON + flag nuevo ON ⇒ alta sin alimento pasa (antes de este cambio, fallaría con "no
  tiene alimento").
- Doble-validación ON + flag nuevo OFF ⇒ sigue fallando igual que hoy (regresión).

### Frontend (smoke manual, doble por empresa — obligatorio por CLAUDE.md §Features por EMPRESA)

- **Flag OFF (Demo/Sanmarino):** cero cambios visibles — abrir Levante y Producción, confirmar que
  mortalidad/sel/alimento siguen pidiendo datos igual que hoy, guardar un seguimiento completo funciona
  igual que antes.
- **Flag ON (Santa Reyes):** en Levante, guardar un seguimiento con **solo mortalidad** cargada (sin
  alimento ni huevos) ⇒ guarda. En Producción, guardar un seguimiento con **solo alimento** cargado (sin
  mortalidad ni huevos) ⇒ guarda. Guardar un seguimiento **completamente vacío** (todo en 0) ⇒ también
  guarda (regla confirmada: sin mínimo). Confirmar en los tres casos que el registro aparece
  correctamente en la grilla diaria (agregación con `permite_multiples_seguimientos_diarios`).

---

## Riesgos identificados (documentados, sin acción requerida en este cambio)

- **Fallback de auto-cálculo de consumo por gramaje en Levante**
  (`SeguimientoLoteLevanteService.Crud.cs:54-68,219-233`): hoy inerte (`IGramajeProvider` registrado
  como `NullGramajeProvider`, siempre devuelve `null`). Si en el futuro se conecta un proveedor real, un
  registro "solo mortalidad" (consumo=0 intencional) podría disparar un auto-cálculo de consumo no
  deseado — revisar en ese momento, no ahora (no hay nada que ejecutar hoy).
- `EnsureDiaSinAporteDeProduccionAsync` / `EnsureDiaSinAporteDeLevanteAsync` (corte de etapa cruzado)
  comparan sumas del día entre las dos tablas — ya toleran filas parciales, pero conviene correr sus
  tests existentes al agregar fixtures de filas parciales, para confirmar que no hay una asunción oculta.

---

## Validación (antes de mergear)

```bash
cd backend && dotnet build && dotnet test
cd frontend && yarn build
```

Gate obligatorio (`CLAUDE.md` §Features por EMPRESA, punto 8): smoke doble por empresa (flag OFF sin
cambios visibles / flag ON cubre los casos nuevos) en **ambos** módulos, Levante y Producción, alta y
edición.
