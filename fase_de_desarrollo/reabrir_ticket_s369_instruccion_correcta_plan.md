# Reabrir `TK-2026-000020` con la instrucción correcta (1-sep-2026)

Último pendiente de [`correccion_hallazgos_auditoria_tickets_plan.md`](correccion_hallazgos_auditoria_tickets_plan.md)
(hallazgo #8). Es el único que **no lleva cambio de código**: lo que está mal es lo que se le dijo al
usuario.

## 1. Qué pasó

El caso se declaró SOLUCIONADO el 14-ago con esta instrucción, textual:

> «Cargar esos 7 dias faltantes (del dia 169 al 175) con la misma plantilla de levante. La
> importacion es idempotente por lote y fecha: las fechas que ya estan cargadas se omiten solas, asi
> que se puede volver a subir el archivo completo sin duplicar nada.»

Es correcta **pero incompleta**, y lo que omite es justamente lo que bloquea: la importación simula el
balance de alimento y **rechaza el archivo entero** si el stock de la granja no alcanza.

El usuario lo intentó el 18-ago, cuatro días después. Quedó registrado en `migracion_masiva`:

| id | archivo | filas | procesadas | omitidas | estado |
|---|---|---|---|---|---|
| 169 | `Carga_Masiva_Levante_S-369A.xlsx` | 0 | 0 | 0 | ConErrores |
| 170 | `Carga_Masiva_Levante_S-369A.xlsx` | 175 | 0 | 0 | ConErrores |
| 171 | `Carga_Masiva_Levante_S-369A V1.xlsx` | 175 | 0 | **168** | ConErrores |

La corrida 171 es la buena: subió el archivo completo, omitió correctamente los 168 días ya cargados
y **no entró ninguno de los 7 restantes**. El único error, textual:

> «No alcanza el stock de POLLA LEVANTE REPRODUCTORA PESADA en la granja: el archivo consume
> 846.500 kg y solo hay 464.190 kg (faltan 382.310 kg).»

Nadie miró esos tres intentos antes de cerrar el caso el 31-ago.

## 2. Estado medido hoy (1-sep-2026)

- **S369A** (`lote_id` 142): **168 días**, 29/08/2025 → 12/02/2026. Faltan del 169 al 175.
- **S369B** (`lote_id` 143): **168 días**, 04/09/2025 → 18/02/2026. Faltan del 169 al 175.
- Stock de `POLLA LEVANTE REPRODUCTORA PESADA` en la granja **MANGOS**: **464,190 kg**, sin moverse
  desde el 12-ago.
- Para **S369B nunca se intentó** la carga de los 7 días: sus únicas corridas son las dos del 12-ago.
  Su déficit, por lo tanto, **no está medido por el sistema**.
- `Agroavicola Sanmarino` tiene `maneja_alimento_por_galpon = false` ⇒ el stock se resuelve **a nivel
  granja**, y los dos sublotes descuentan del **mismo saldo**.

## 3. Por qué se reabre el mismo caso y no se registra uno nuevo

`CERRADO` es **terminal** en la máquina de estados (`TicketEstados.Transiciones[Cerrado]` está vacío)
y hay tests que lo blindan. La regla existe porque un caso cerrado lo cerraron **las dos partes**.

Acá eso no pasó: el caso lo cerró **la gestión** —la migración `20260831130000`, ayer—, el solicitante
nunca confirmó nada y ni siquiera se le envió el aviso de solución (`notificado_correo = false`).
Reabrirlo no rompe el invariante: **repone el estado que una migración le impuso sin que la otra parte
participara**. La máquina de estados de la aplicación no se toca.

Estado destino: **`EN_ANALISIS`**, que es el que la propia máquina define para la reapertura
(`SOLUCIONADO → EN_ANALISIS`).

## 4. La instrucción correcta

Reemplaza a la anterior en `solucion_descripcion` y se repite en la nota de reapertura:

1. **Primero** registrar la entrada de alimento que falta: `POLLA LEVANTE REPRODUCTORA PESADA` en la
   granja MANGOS, con fecha **anterior o igual** al primer día faltante, por los kilos que realmente
   entraron. Se puede hacer en la hoja «Alimento» del mismo archivo o por el módulo de inventario.
2. **Recién después** subir el archivo completo de 175 filas. Los 168 días ya cargados se omiten
   solos; entran los 7 nuevos.
3. **Antes de importar, usar «Validar»** (dry-run): corre la misma simulación de balance sin escribir
   nada y dice el déficit exacto. Es lo que evita un tercer intento a ciegas.
4. **Los dos sublotes descuentan del mismo stock de granja**, así que la entrada tiene que cubrir a
   S369A y a S369B, o se cargan dos entradas.
5. Déficit conocido: **382.310 kg** para S369A, medido por el propio sistema el 18-ago. El de S369B
   **no está medido** —nunca se intentó esa carga—; sale del dry-run del paso 3.

⚠️ El guard de stock **no es un bug**: es un invariante correcto, y por eso la salida es cargar el
alimento que falta, no saltearlo.

## 5. Alcance

- Migración data-only: reabre el caso, reescribe la solución y agrega la nota. Idempotente y
  fail-safe: si alguien ya lo movió de `CERRADO`, no lo toca.
- **No** se corrige ningún dato de levante ni de inventario: la carga es del usuario, y el sistema ya
  hace bien lo suyo (la idempotencia por lote+fecha hace que reimportar sea seguro).
