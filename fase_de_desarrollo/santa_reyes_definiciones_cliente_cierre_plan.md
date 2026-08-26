# Santa Reyes — cierre de las definiciones del cliente (24-ago-2026)

Continúa [`santa_reyes_requerimientos_italapp_plan.md`](santa_reyes_requerimientos_italapp_plan.md).
Cierra 3 de las 5 definiciones que quedaban bloqueadas en `TK-2026-000180` (`SR-DEF-1`, `SR-DEF-5`,
`SR-DEF-6`) y corrige un bug de clasificación de raza que destapó el archivo del cliente.

## 0. Fuentes de esta sesión (documentos del cliente, no del repo)

| Archivo | Qué aporta |
|---|---|
| `Desktop\Requerimiento Santa reyes\Items.xlsx` (hoja «Items Huevo») | Los **21 códigos ERP reales** del catálogo de huevo. **No contiene ningún «Enyemado»** |
| `Desktop\Requerimiento Santa reyes\Lotes.xlsx` | Los 10 lotes con su **raza y `Tipo Ave` (ROJA/BLANCA)** — es la fuente que resuelve la clasificación |
| `Desktop\Requerimiento Santa reyes\Granja.xlsx` | Estructura física: 38 silos, 1 bodega (`BUG60100` · «BUG. PRINCIPAL GR. ESPERANZA»), 38 galpones |
| `Downloads\Requerimientos de Italapp.docx` | Texto fuente del requerimiento (secciones MORTALIDADES, TRASLADO DE AVES, TRASLADO DE HUEVOS, LEVANTES) |

## 1. Decisiones tomadas por el usuario en sesión (24-ago-2026)

1. **Machos (`SR-DEF-1` / F5.3)** — *«Santa Reyes no maneja nada de macho, no debería contar doble ni
   nada; ocultar todos esos campos, no se utilizan en ninguna parte»*. ⇒ La lectura correcta del
   `.docx` («que en ventas aparezca campo machos sobre el total de las aves») **no** es agregar un
   campo informativo: es **retirar machos también de ventas**. Se extiende el flag existente
   `ocultaMachosEnPostura`; **no se crea flag nuevo**.
2. **Lohmann Brown** — dejarla **sin guía genética** (no se inventan sus 123 filas). Solo se corrige
   su **clasificación de grupo**.
3. **Grafía de razas** — el sistema **tolera la grafía del ERP** (alias de lectura). **No** se
   modifica el dato que vino del ERP del cliente.
4. **Enyemado / Decolorado sin código ERP** — quedan **sin código y ocultos**: no se ofrecen para
   clasificar hasta que el cliente entregue los códigos. No se borran ni se inventan códigos.

## 2. Bug encontrado y probado: `LOHMANN BROWN` se clasifica como blanca

`SemanasCicloPosturaCalculos.EsGrupoBlancaAzur` evalúa **primero** `TokensBlancaAzur = {LOHMANN, AZUR}`
con `Contains`. `"LOHMANN BROWN"` contiene `"LOHMANN"` ⇒ devuelve `true` (blanca, 84 semanas de
postura, fin de ciclo en la **112**). El `Lotes.xlsx` del cliente dice que **LOHMANN BROWN es ROJA**
⇒ le corresponden 74 semanas y fin de ciclo en la **102**. Afecta al lote 229 (`G3001229`), que existe
hoy en `lote_postura_base`.

**Fix:** invertir el orden de evaluación (Rojas/Criollas primero) y agregar el token `BROWN`.
Verificación de las 6 razas conocidas con el orden nuevo:

| Raza | Token que matchea | Grupo resultante | ¿Correcto? |
|---|---|---|---|
| `Babcock Brown` / `BABCOK BROWN` | BABCOCK / BABCOK / BROWN | Roja | ✅ igual que hoy |
| `Hy Line Brown` / `HY LINE` | HY LINE / BROWN | Roja | ✅ igual que hoy |
| `Lohmann Brown` / `LOHMANN BROWN` | **BROWN** | **Roja** | 🔴 **CAMBIA** — hoy da blanca (mal) |
| `Lohmann LSL` | LOHMANN | Blanca | ✅ igual que hoy |
| `Azur` | AZUR | Blanca | ✅ igual que hoy |
| `Criolla` | CRIOLLA | Roja | ✅ igual que hoy |

El único cambio de comportamiento es el que corrige el defecto. El cálculo está gateado por
`Company.SemanasCicloPosturaPorRaza`, ON solo en Santa Reyes ⇒ **cero impacto multiempresa**.

## 3. Las 3 razas que no cruzan con la guía

Medido en BD local (`lote_postura_base` de company 6 ⟕ `guia_genetica_santa_reyes`):

| Raza en el lote (grafía ERP) | ¿Cruza con la guía? |
|---|---|
| `BABCOK BROWN` | ❌ la guía dice `Babcock Brown` |
| `HY LINE` | ❌ la guía dice `Hy Line Brown` |
| `LOHMANN BROWN` | ❌ no existe en la guía (decisión 2: queda así) |
| `LOHMANN LSL` | ✅ |

⇒ **3 de 4 razas de los lotes reales no encuentran su guía** y los reportes técnicos salen sin
columnas de comparación. Se resuelve con alias de lectura (decisión 3).

## 4. Alcance por paquete

### W1 · Machos fuera de postura (`SR-DEF-1` / F5.3)

Flag existente `ocultaMachosEnPostura`. Se aplica por **fases**, de mayor a menor riesgo de dato
incorrecto:

- **W1.a — captura (donde puede «contar doble»)**: `modal-movimiento-aves` (incluye **Venta**),
  `inventario-dashboard` (traslado y retiro), `traslado-aves-huevos`, `modal-registro-inicial`,
  `lote-list` (`machosL`, `cantidadMachos`), `seguimiento-lote-levante-list` (aves machos para
  producción).
- **W1.b — residuos de los modales ya gateados**: `modal-traslado-aves-seguimiento` (machos vivos,
  pills de ingreso/salida) y **verificar la anidación sospechosa** de `errorSexajeMachos` en
  `modal-seguimiento-diario` y `modal-create-edit`.
- **W1.c — tablas y tabs**: `tabs-principal` (levante y producción), listados de movimientos.
- **W1.d — reportes y exportaciones a Excel**: fuera del alcance de esta sesión salvo que sobre
  tiempo; se deja inventariado en el tracker.

⚠️ **Nunca** se toca el modelo de datos ni el payload: los saldos consumen esos campos. Es
ocultamiento de UI, exactamente como F5.1/F5.2.
⚠️ Engorde y reproductora **sí** manejan machos legítimamente — no se tocan.

### W2 · Línea genética

- `SemanasCicloPosturaCalculos` (backend) + `semanas-ciclo-postura.funcion.ts` (front): orden de
  evaluación invertido + token `BROWN`. Tests en los dos lados.
- Alias de grafía ERP → guía, en un cálculo puro nuevo `RazaGuiaAliasCalculos`, consumido por
  `GuiaGeneticaLookup` y `GuiaGeneticaService`. Alias: `BABCOK BROWN → Babcock Brown`,
  `HY LINE → Hy Line Brown`. Normalización case/espacios.

### W3 · Comprobante de traslado de aves (`SR-DEF-5` / F9.2c)

Sería el **primer comprobante del repo**. Se copia el patrón de
`indicador-ecuador/components/liquidacion-reporte-panama` (componente standalone + `@Input()` +
`print()` + `@media print`). **Sin librería de PDF** — no hay ninguna en el repo y no hace falta.

- Fuente de datos: `GET api/TrasladoNavigation/{id}` → `MovimientoAvesCompletoDto` (ya trae origen y
  destino completos + placa/conductor/sellos).
- Gap a cerrar: la interfaz TS `MovimientoAvesCompleto` **no declara** `placa`/`conductor`/`sellos`
  aunque el backend los envía.
- Respeta `ocultaMachosEnPostura`.
- Punto de entrada: botón por fila en el listado de movimientos de aves.

### W4 · Bodega de salida por lista maestra (`SR-DEF-6` / F10.1)

Requerimiento literal: *«…no vemos bien que la bodega de salida sea digitada… debe ser una lista
desplegable, de solo las opciones de traslado que tenga la granja»*. Decisión del usuario: **vive en
el módulo de listas maestras** y se siembra una **bodega general** para Santa Reyes.

- Hoy Santa Reyes tiene **una sola** lista maestra (`region_option_key`); le faltan las 4 de traslado
  de huevos y la de movimiento de aves. Ecuador (company 3) es la referencia poblada.
- Migración data-only idempotente que siembra para Santa Reyes:
  `traslado_de_huevos_planta_destino` (con la bodega general), `traslado_de_huevos_tipo_destino`,
  `traslado_de_huevos_tipo_de_operacion`, `traslado_de_huevos_venta_motivo`,
  `movimiento_de_aves_tipo_movimiento`.
- Front: en `modal-traslado-huevos` el destino deja de ser exclusivo de la operación **Venta** — en
  **Traslado** se ofrece el mismo desplegable alimentado por la lista maestra. Nada de texto libre.

## 5. Casos de prueba

**W2 (xUnit + Karma, espejados):**
- `Lohmann Brown` semana 102 ⇒ `Postura`; semana 103 ⇒ `FueraDeCiclo` (hoy da `Postura` hasta 112).
- `LOHMANN BROWN` (mayúsculas, grafía ERP) ⇒ mismo resultado.
- `Lohmann LSL` semana 112 ⇒ `Postura`; 113 ⇒ `FueraDeCiclo` (**sin cambio**).
- `Babcock Brown`, `BABCOK BROWN`, `Hy Line Brown`, `HY LINE`, `Criolla` ⇒ Roja (**sin cambio**).
- `Azur` ⇒ Blanca (**sin cambio**). `Ross 308` ⇒ `null` (**sin cambio**).
- Alias: `BABCOK BROWN` → `Babcock Brown`; `HY LINE` → `Hy Line Brown`; `  lohmann lsl  ` →
  `Lohmann LSL`; raza desconocida ⇒ se devuelve tal cual (no se inventa).

**W4:** migración corrida **dos veces** seguidas deja el mismo número de filas (idempotencia), y el
`Down` probado dentro de una transacción revertida.

**W1/W3:** `yarn build` + smoke visual con empresa **OFF** (Sanmarino: cero cambios) y **ON**
(Santa Reyes).

## 6. Lo que esta sesión NO cierra

- `SR-DEF-3` (F8.1) — los 7 ítems PNC siguen sin código ERP por decisión 4.
- `SR-DEF-4` (F8.3) — panel de eficiencia, depende de F8.1.
- `F11.3` — pruebas asistidas con el cliente.
- La guía genética de `Lohmann Brown` (decisión 2).
- `ActualizarTrasladoHuevosAsync` sigue sin tocar `metadata->huevoItems` (gap preexistente).
