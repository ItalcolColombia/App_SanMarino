/**
 * ¿Registrar HOY esta vacuna quedaría fuera de la franja programada?
 *
 * Espejo exacto de `VacunacionCalculos.ProyectarAplicacion` (backend). Se duplica a propósito: el
 * modal necesita responderlo **antes** de guardar, para desplegar la novedad y pedir el motivo en
 * vez de dejar que el usuario se entere por un 400.
 *
 * ⚠️ La base del día es **UTC**, no la local, porque el servidor sella con `DateTime.UtcNow.Date`.
 * En Ecuador/Colombia (UTC−5) el navegador está en otro día entre las 19:00 y la medianoche: con la
 * fecha local, la UI diría "dentro de franja" mientras el backend calcula +1 d, exige motivo y
 * devuelve el mismo 400 que esto viene a eliminar.
 *
 * El backend sigue siendo la autoridad: esto adelanta el aviso, no reemplaza la validación.
 */

export interface EvaluacionAplicacionHoy {
  /** true ⇒ el backend va a exigir motivo. */
  fueraDeRango: boolean;
  /** Positivo = tardía (días después del fin de franja) · negativo = adelantada · 0 = dentro. */
  diasDesviacion: number;
  /** Por qué queda fuera, listo para mostrar. `null` cuando está dentro de franja. */
  mensaje: string | null;
}

const MS_POR_DIA = 86_400_000;

/** Fecha de hoy en la MISMA base que el servidor (UTC), como 'YYYY-MM-DD'. */
export function hoyEnBaseDelServidor(ahora: Date = new Date()): string {
  return ahora.toISOString().slice(0, 10);
}

/**
 * Convierte una fecha del API a milisegundos UTC de su día. Se queda con los 10 primeros
 * caracteres ('YYYY-MM-DD'): las franjas son fechas puras y el sufijo horario —lo traiga o no—
 * no debe correr el día.
 */
function diaUtc(fecha: string): number {
  const [anio, mes, dia] = fecha.slice(0, 10).split('-').map(Number);
  return Date.UTC(anio, mes - 1, dia);
}

export function evaluarAplicacionHoy(
  fechaInicioFranja: string | null | undefined,
  fechaFinFranja: string | null | undefined,
  hoy: string = hoyEnBaseDelServidor(),
): EvaluacionAplicacionHoy {
  // Sin franja no hay nada que exigir: el backend decide y, si hace falta, responde el motivo.
  if (!fechaInicioFranja || !fechaFinFranja) {
    return { fueraDeRango: false, diasDesviacion: 0, mensaje: null };
  }

  const inicio = diaUtc(fechaInicioFranja);
  const fin = diaUtc(fechaFinFranja);
  const dia = diaUtc(hoy);

  if (dia < inicio) {
    const dias = Math.round((inicio - dia) / MS_POR_DIA);
    return {
      fueraDeRango: true,
      diasDesviacion: -dias,
      mensaje: `La franja abre en ${dias} ${dias === 1 ? 'día' : 'días'}: se va a registrar como aplicación adelantada.`,
    };
  }

  if (dia > fin) {
    const dias = Math.round((dia - fin) / MS_POR_DIA);
    return {
      fueraDeRango: true,
      diasDesviacion: dias,
      mensaje: `La franja cerró hace ${dias} ${dias === 1 ? 'día' : 'días'}: se va a registrar como aplicación tardía.`,
    };
  }

  return { fueraDeRango: false, diasDesviacion: 0, mensaje: null };
}
