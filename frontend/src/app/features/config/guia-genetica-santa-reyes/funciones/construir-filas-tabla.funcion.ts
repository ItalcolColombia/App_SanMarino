// src/app/features/config/guia-genetica-santa-reyes/funciones/construir-filas-tabla.funcion.ts
/**
 * Arma las filas del grid a partir de los DTOs del backend. Función **pura**: sin `this`, sin DI,
 * sin toast ni HTTP.
 *
 * Se llama **una vez** por respuesta y el resultado se guarda en un campo del componente. No es un
 * getter: un getter que devuelve un array nuevo por ciclo rompe el change detection (regla del
 * repo) y acá además re-formatearía 615 filas en cada tick.
 */
import {
  FilaGuiaGeneticaSantaReyes,
  GuiaGeneticaSantaReyesDto,
  SEMANA_COBERTURA_MAX,
  SEMANA_COBERTURA_MIN
} from '../models/guia-genetica-santa-reyes.model';

/** Guion largo — el mismo que usa el resto del front para «no hay dato». */
const SIN_DATO = '—';

/**
 * Métrica de la guía con **2 decimales fijos**, en formato es-CO.
 *
 * 🔴 `null` ⇒ `—`, **jamás `0,00`**: en esta guía «sin dato» y «cero» son cosas distintas —la raza
 * Criolla tiene 40 semanas (101–140) con `prod_porcentaje` legítimamente nulo—, y pintar un 0 ahí
 * le diría al usuario que esa semana la línea no produce.
 *
 * No usa `formatearNumero` de `shared/utils/format` porque aquél no lleva decimales (1234 → «1.234»)
 * ni `formatDecimalTrim` porque aquél recorta los ceros finales (5,90 → «5,9») y devuelve el punto
 * decimal de JS; la guía se lee en columnas y necesita los decimales alineados.
 */
export function formatearMetricaGuia(valor: number | null | undefined): string {
  if (valor === null || valor === undefined || !Number.isFinite(valor)) return SIN_DATO;
  return new Intl.NumberFormat('es-CO', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(valor);
}

/** ¿La semana cae fuera del tramo que cubre la guía de producción (18–140)? */
export function estaFueraDeCobertura(edad: number | null | undefined): boolean {
  if (edad === null || edad === undefined || !Number.isFinite(edad)) return true;
  return edad < SEMANA_COBERTURA_MIN || edad > SEMANA_COBERTURA_MAX;
}

/** DTOs → filas pintables. Devuelve un array nuevo; no muta la entrada. */
export function construirFilasTablaGuia(
  items: readonly GuiaGeneticaSantaReyesDto[] | null | undefined
): FilaGuiaGeneticaSantaReyes[] {
  if (!items?.length) return [];

  return items.map(item => ({
    id: item.id,
    raza: item.raza?.trim() || SIN_DATO,
    anioGuia: item.anioGuia?.trim() || SIN_DATO,
    edad: item.edad,
    edadTexto: Number.isFinite(item.edad) ? `S ${item.edad}` : SIN_DATO,
    prodPorcentajeTexto: formatearMetricaGuia(item.prodPorcentaje),
    retiroAcHTexto: formatearMetricaGuia(item.retiroAcH),
    grAveDiaHTexto: formatearMetricaGuia(item.grAveDiaH),
    codigoGuiaGenetica: item.codigoGuiaGenetica?.trim() || SIN_DATO,
    fueraDeCobertura: estaFueraDeCobertura(item.edad),
    origen: item
  }));
}
