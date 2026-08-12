// Función PURA: decide si el consumo de alimento de una fila del seguimiento diario pollo engorde
// viene DESGLOSADO POR GÉNERO o es una ración MIXTA para todo el galpón.
// Sin `this`, sin DI, sin servicios — recibe la fila, devuelve el modo.
//
// Por qué existe (jul/ago-2026, reportado por Panamá con el Excel del lote «94 - 2»):
//   · Días 1–7: la fila la genera `fn_cruce_reproductora_a_engorde` desde los lotes reproductora, que
//     sí traen consumo por sexo. Esas filas nacen con `origen_cruce = true` Y
//     `created_by_user_id = 'SYSTEM_CRUCE'` (correspondencia 1:1 en los datos: 203/203).
//   · Día 8 en adelante: la fila la crea el usuario desde este módulo. El alimento ya es una sola
//     ración mixta, pero se persiste en `consumo_kg_hembras` con machos en 0 (Panamá vía
//     `mapearPanamaMixtoAHM`; Ecuador de hecho hace lo mismo). Mostrado bajo «Consumo hembras» se
//     lee como si solo comieran las hembras.
//
// La decisión NO depende del país ni del número de día: depende del ORIGEN de la fila, que ya viaja
// al front en `createdByUserId` (el template lo usa para el badge «🔄 Auto»). `origen_cruce` no está
// en el RETURNS TABLE de `fn_seguimiento_diario_engorde` y exponerlo obligaría a recrear la función.

/** `'genero'` = consumo desglosado H/M · `'mixto'` = una sola ración para el galpón. */
export type ModoConsumoAlimento = 'genero' | 'mixto';

/** Autor con el que la función de cruce firma las filas que copia desde los lotes reproductora. */
export const USUARIO_CRUCE_REPRODUCTORA = 'SYSTEM_CRUCE';

/** Tipo estructural: acepta la fila real de la tabla diaria sin acoplarse al service. */
export interface FilaConsumoAlimentoLike {
  readonly consumoKgMachos?: number | null;
  readonly createdByUserId?: string | null;
}

/**
 * Modo de consumo de la fila. `SYSTEM_CRUCE` manda; el consumo de machos > 0 queda como red de
 * seguridad para las filas que perdieron el autor (copia congelada de un lote liquidado) o para un
 * registro cargado con desglose real por sexo.
 */
export function modoConsumoAlimentoFila(f: FilaConsumoAlimentoLike): ModoConsumoAlimento {
  if ((f.createdByUserId ?? '').trim() === USUARIO_CRUCE_REPRODUCTORA) return 'genero';
  if ((f.consumoKgMachos ?? 0) > 0) return 'genero';
  return 'mixto';
}

/** true cuando la fila trae consumo por sexo (columnas Hembras/Machos). */
export function esConsumoAlimentoPorGenero(f: FilaConsumoAlimentoLike): boolean {
  return modoConsumoAlimentoFila(f) === 'genero';
}

/** true cuando la fila trae una ración mixta (columna Mixto). */
export function esConsumoAlimentoMixto(f: FilaConsumoAlimentoLike): boolean {
  return modoConsumoAlimentoFila(f) === 'mixto';
}
