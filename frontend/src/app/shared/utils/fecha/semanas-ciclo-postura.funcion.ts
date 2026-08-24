/**
 * Etapa del ciclo de vida del ave por semana de edad (desde encasetamiento) y por raza — solo
 * empresas con `Company.semanasCicloPosturaPorRaza` = true (hoy, Santa Reyes).
 *
 * Funciones PURAS (sin `this`, sin DI, sin estado). Espejo de
 * `ZooSanMarino.Application.Calculos.SemanasCicloPosturaCalculos` (backend) — mismos cortes, mismos
 * literales. Cualquier cambio de umbral se hace en LOS DOS lados.
 *
 * Fuente: `Requerimientos de Italapp.docx` (sección «Consumo de alimento», 18-ago-2026) — "Desde la
 * creación del Item, son 8 sem en alistamiento, y 16 semanas en etapa de levante... 4 semanas en
 * etapa levante pero en granjas de producción y 74/84 semanas en etapa de postura". Los cuatro
 * primeros cortes son iguales para los dos grupos de raza; solo la duración de la postura difiere.
 *
 * La semana de vida se calcula con la misma fórmula canónica que `semanaVidaLevante`
 * (`floor(dias/7)+1`, fechas ancladas a mediodía local) — se reusa esa función en vez de duplicar el
 * cálculo de fecha por tercera vez en el repo (la otra es la de
 * `modal-seguimiento-diario.component.ts:calcularEtapa`, que NO se toca: sigue la etapa 1/2/3 vieja
 * cuando el flag está apagado).
 */
import { semanaVidaLevante } from '../../../features/lote-levante/funciones/semana-vida-levante.funcion';

export const ETAPA_CICLO_ALISTAMIENTO = 'Alistamiento';
export const ETAPA_CICLO_LEVANTE = 'Levante';
export const ETAPA_CICLO_LEVANTE_EN_PRODUCCION = 'LevanteEnProduccion';
export const ETAPA_CICLO_POSTURA = 'Postura';
export const ETAPA_CICLO_FUERA_DE_CICLO = 'FueraDeCiclo';

export type EtapaCicloPostura =
  | typeof ETAPA_CICLO_ALISTAMIENTO
  | typeof ETAPA_CICLO_LEVANTE
  | typeof ETAPA_CICLO_LEVANTE_EN_PRODUCCION
  | typeof ETAPA_CICLO_POSTURA
  | typeof ETAPA_CICLO_FUERA_DE_CICLO;

const SEMANAS_ALISTAMIENTO = 8;
const SEMANAS_LEVANTE = 16;
const SEMANAS_LEVANTE_EN_PRODUCCION = 4;
const SEMANAS_POSTURA_ROJA_CRIOLLA = 74;
const SEMANAS_POSTURA_BLANCA_AZUR = 84;

const FIN_ALISTAMIENTO = SEMANAS_ALISTAMIENTO; // 8
const FIN_LEVANTE = FIN_ALISTAMIENTO + SEMANAS_LEVANTE; // 24
const FIN_LEVANTE_EN_PRODUCCION = FIN_LEVANTE + SEMANAS_LEVANTE_EN_PRODUCCION; // 28

const TOKENS_BLANCA_AZUR = ['LOHMANN', 'AZUR'];
/**
 * `BROWN` nombra al ave marrón en las tres líneas que el cliente maneja (Babcock Brown, Hy Line
 * Brown, Lohmann Brown) y es lo que desempata a `Lohmann Brown` — ver `esGrupoBlancaAzur`.
 */
const TOKENS_ROJA_CRIOLLA = ['BABCOCK', 'BABCOK', 'HY LINE', 'HYLINE', 'CRIOLLA', 'BROWN'];

/**
 * ¿La raza pertenece al grupo Blancas/Azur (84 semanas de postura)? `null` si la raza es
 * nula/vacía o no coincide con ningún token conocido — no se adivina el grupo.
 *
 * 🔴 **El orden importa y no se puede invertir.** Los tokens se buscan por `includes`, y
 * `"Lohmann Brown"` contiene `"LOHMANN"`: evaluando primero Blancas/Azur se clasificaba como
 * blanca (84 semanas, fin de ciclo en la 112) cuando el `Lotes.xlsx` del cliente la declara `ROJA`
 * (74 semanas, fin de ciclo en la 102). Rojas/Criollas va primero porque su token `BROWN` es el más
 * específico: `Lohmann LSL` (blanca) no lo tiene y sigue cayendo en `LOHMANN`, como siempre.
 */
export function esGrupoBlancaAzur(raza: string | null | undefined): boolean | null {
  if (!raza || !raza.trim()) return null;
  const r = raza.trim().toUpperCase();

  if (TOKENS_ROJA_CRIOLLA.some(t => r.includes(t))) return false;
  if (TOKENS_BLANCA_AZUR.some(t => r.includes(t))) return true;

  return null;
}

/**
 * Etapa del ciclo de vida para esta raza a esta semana de vida (1-based, ver `semanaVidaLevante`).
 * `null` si la raza no se reconoce o la semana es menor a 1.
 */
export function obtenerEtapaCicloPostura(
  raza: string | null | undefined,
  semanaVida: number | null | undefined
): EtapaCicloPostura | null {
  if (semanaVida == null || semanaVida < 1) return null;

  const esBlancaAzur = esGrupoBlancaAzur(raza);
  if (esBlancaAzur === null) return null;

  if (semanaVida <= FIN_ALISTAMIENTO) return ETAPA_CICLO_ALISTAMIENTO;
  if (semanaVida <= FIN_LEVANTE) return ETAPA_CICLO_LEVANTE;
  if (semanaVida <= FIN_LEVANTE_EN_PRODUCCION) return ETAPA_CICLO_LEVANTE_EN_PRODUCCION;

  const semanasPostura = esBlancaAzur ? SEMANAS_POSTURA_BLANCA_AZUR : SEMANAS_POSTURA_ROJA_CRIOLLA;
  const finPostura = FIN_LEVANTE_EN_PRODUCCION + semanasPostura;

  return semanaVida <= finPostura ? ETAPA_CICLO_POSTURA : ETAPA_CICLO_FUERA_DE_CICLO;
}

/** Etiqueta legible para mostrar en el formulario. */
export function etiquetaEtapaCicloPostura(etapa: EtapaCicloPostura | null): string {
  switch (etapa) {
    case ETAPA_CICLO_ALISTAMIENTO: return 'Alistamiento';
    case ETAPA_CICLO_LEVANTE: return 'Levante';
    case ETAPA_CICLO_LEVANTE_EN_PRODUCCION: return 'Levante en producción';
    case ETAPA_CICLO_POSTURA: return 'Postura';
    case ETAPA_CICLO_FUERA_DE_CICLO: return 'Fuera de ciclo';
    default: return '—';
  }
}

/** Semana de vida (1-based) + etapa, en un solo llamado — azúcar para los componentes. */
export function calcularEtapaCicloPostura(
  raza: string | null | undefined,
  fechaEncaset: string | Date | null | undefined,
  fechaRegistro: string | Date | null | undefined
): EtapaCicloPostura | null {
  const semana = semanaVidaLevante(fechaEncaset, fechaRegistro);
  return obtenerEtapaCicloPostura(raza, semana);
}
