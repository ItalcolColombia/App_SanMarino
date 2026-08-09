import type { EstadoSw } from '../models/pwa.model';

/**
 * Traduce el estado crudo del Service Worker a una etiqueta y un semáforo.
 *
 * Función PURA: recibe los tres booleanos ya leídos del navegador, no los lee.
 *
 * ## El caso que importa: "registrado pero no controla"
 *
 * En el **primer** load de la vida de la app eso es normal y esperado — el SW se instala
 * mientras la página ya se está sirviendo de la red, y recién toma el control en la próxima
 * navegación. A partir del **segundo** load, en cambio, es el síntoma exacto de que el SW
 * arrancó en **safe mode y se desactivó solo**: pasa cuando el SHA1 de un archivo no coincide
 * con el declarado en `ngsw.json` (típicamente porque algo reescribió el output después del
 * build). El síntoma es que la app funciona perfecto con red y no funciona nada sin red, sin
 * un solo error en consola.
 *
 * Por eso el resumen necesita saber si es el primer load: la misma combinación de booleanos
 * significa "todo bien, esperá" o "esto está roto" según ese dato.
 */
export function resumirEstadoSw(entrada: {
  soportado: boolean;
  registrado: boolean;
  controlando: boolean;
  /** `true` si es la primera visita en este navegador (nunca hubo un SW controlando). */
  primerLoad?: boolean;
}): EstadoSw {
  const { soportado, registrado, controlando, primerLoad = false } = entrada;

  const base = { soportado, registrado, controlando };

  if (!soportado) {
    return {
      ...base,
      etiqueta: 'No soportado por este navegador',
      severidad: 'aviso'
    };
  }

  if (!registrado) {
    return {
      ...base,
      etiqueta: 'Sin registrar (build de desarrollo o registro pendiente)',
      severidad: 'aviso'
    };
  }

  if (controlando) {
    return {
      ...base,
      etiqueta: 'Activo y controlando la app',
      severidad: 'ok'
    };
  }

  if (primerLoad) {
    return {
      ...base,
      etiqueta: 'Instalándose (toma el control al recargar)',
      severidad: 'aviso'
    };
  }

  return {
    ...base,
    etiqueta: 'Registrado pero NO controla — posible safe mode',
    severidad: 'error'
  };
}
