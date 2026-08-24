// src/app/features/lote/funciones/lote-list-encasetamiento.funcion.ts
// Edad, fase visible y encasetamiento de un lote — extraído de LoteListComponent.
// Funciones PURAS: sin `this`, sin DI, sin estado del componente.

import { LoteDto } from '../services/lote.service';
import { LotePosturaLevanteDto } from '../services/lote-postura-levante.service';
import { LotePosturaProduccionDto } from '../services/lote-postura-produccion.service';

/** Edad en días desde fechaEncaset (día del encaset = día 1). */
export function calcularEdadDias(fechaEncaset?: string | Date | null): number {
  if (!fechaEncaset) return 0;
  const inicio = new Date(fechaEncaset);
  const hoy = new Date();
  const msDia = 1000 * 60 * 60 * 24;
  return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
}

/** Edad en semanas desde fechaEncaset. Día 0 = semana 1, días 7-13 = semana 2, etc. */
export function calcularEdadSemanas(fechaEncaset?: string | Date | null): number {
  if (!fechaEncaset) return 1;
  const inicio = new Date(fechaEncaset);
  const hoy = new Date();
  const msSem = 1000 * 60 * 60 * 24 * 7;
  const semanas = Math.floor((hoy.getTime() - inicio.getTime()) / msSem);
  return Math.max(1, semanas + 1); // primera semana = 1, no 0
}

/**
 * Fase que se muestra en pantalla, tomada del ESTADO del lote y no de su edad.
 *
 * Un lote pasa a «Producción» solo cuando ocurrieron las dos cosas: su levante se cerró y existe
 * el lote de producción. Mientras falte cualquiera de las dos sigue siendo «Levante», así que la
 * palabra «Producción» no aparece hasta que el lote está de verdad en esa etapa.
 *
 * Antes esto era `edad < 26 semanas ? Levante : Producción`, que es correcto para un lote dado de
 * alta al día y falso para todo lote cargado con historia: su encasetamiento es viejo, así que
 * nacía mostrando «Producción» sin haber pasado nunca a producción. Medido en la base: 8 de los
 * 16 lotes de Sanmarino, todos con el levante abierto y cero filas de producción.
 *
 * La resuelve el backend (`FaseLoteCalculos.ResolverFaseVisible`) y viaja en `faseActual`. Sin
 * ese campo se devuelve «—»: es preferible no decir nada a volver a adivinar por la fecha.
 */
export function calcularFase(l?: LoteDto | null): string {
  const fase = (l?.faseActual ?? '').trim();
  if (!fase) return '—';
  return fase.toLowerCase() === 'produccion' ? 'Producción' : 'Levante';
}

// ─── Encasetamiento en las grillas de Levante y Producción ──────────────────
// Las columnas «Hembras encaset.» / «Machos encaset.» mostraban `avesHActual`/`avesMActual`, o
// sea el SALDO que el seguimiento diario va descontando: bajaban solas y sumaban MENOS que la
// columna «Aves encaset.» de al lado. El encasetamiento es histórico del lote y no se mueve.
// El desglose inicial vs. actual sigue completo en el panel de detalle de cada lote.
//
// ⚠️ La fuente es `hembrasL`/`machosL`, NO `avesHInicial`: en PRODUCCIÓN son dos eventos
// distintos y sólo el primero es el encasetamiento. `avesHInicial` son las aves con que arrancó
// producción, o sea las que sobrevivieron al levante — medido en P-K345B: encasetamiento 12.587
// (10.991 + 1.596) contra un inicio de producción de 11.526. Usar `avesHInicial` en una columna
// rotulada «encaset.» la dejaba sin cuadrar con el total de al lado.
// En LEVANTE los dos coinciden por construcción (el trigger espeja `aves_h_inicial = hembras_l`;
// verificado: 21 de 21 lotes), así que el respaldo sólo cubre filas sin base cargada.
// El desglose inicio-de-producción vs. actual sigue completo en el panel de detalle.

/** Hembras con que se encasetó un lote de levante. */
export function encasetHembrasLevante(l: LotePosturaLevanteDto): number {
  return l.hembrasL ?? l.avesHInicial ?? 0;
}

/** Machos con que se encasetó un lote de levante. */
export function encasetMachosLevante(l: LotePosturaLevanteDto): number {
  return l.machosL ?? l.avesMInicial ?? 0;
}

/** Hembras con que se encasetó un lote que hoy está en producción. */
export function encasetHembrasProduccion(l: LotePosturaProduccionDto): number {
  return l.hembrasL ?? l.avesHInicial ?? l.hembrasInicialesProd ?? 0;
}

/** Machos con que se encasetó un lote que hoy está en producción. */
export function encasetMachosProduccion(l: LotePosturaProduccionDto): number {
  return l.machosL ?? l.avesMInicial ?? l.machosInicialesProd ?? 0;
}

/**
 * Total encasetado. Prefiere `avesEncasetadas` (la columna que el lote declara) y solo reconstruye
 * desde el desglose si falta. **El fallback nunca usa el saldo**, que es lo que hacía que el total
 * quedara por debajo de las aves realmente encasetadas.
 */
export function encasetTotal(declarado: number | null | undefined, hembras: number, machos: number): number {
  return (declarado ?? 0) > 0 ? declarado! : hembras + machos;
}

/** Normaliza estadoCierre para Levante (Abierto/Cerrado). Comparación insensible a mayúsculas. */
export function estadoCierreLevante(l: LotePosturaLevanteDto): 'Abierto' | 'Cerrado' {
  const v = (l.estadoCierre ?? 'Abierto').toString().trim().toLowerCase();
  return v === 'cerrado' ? 'Cerrado' : 'Abierto';
}

/** Normaliza estadoCierre para Producción (Abierto/Cerrado). Comparación insensible a mayúsculas. */
export function estadoCierreProduccion(l: LotePosturaProduccionDto): 'Abierto' | 'Cerrado' {
  const v = (l.estadoCierre ?? 'Abierta').toString().trim().toLowerCase();
  return v === 'cerrada' ? 'Cerrado' : 'Abierto';
}
