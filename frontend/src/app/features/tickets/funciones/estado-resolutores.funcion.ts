// features/tickets/funciones/estado-resolutores.funcion.ts
//
// Pasa las filas de resolutor que devuelve el backend al estado que dibuja el editor (un toggle y un
// alcance por tipo) y al resumen de solo lectura de «qué atiende hoy».
//
// Funciones PURAS: sin `this`, sin DI, sin HTTP. La pantalla solo las llama.

/** Alcance de una fila: atiende su empresa o todas. */
export type AlcanceResolutorVista = 'EMPRESA' | 'GLOBAL';

/** Lo que interesa de una fila de `ticket_resolutores` / `ticket_resolutor_rol`. */
export interface FilaResolutorVista {
  tipo: string;
  paisId: number | null;
  activo: boolean;
  alcance?: AlcanceResolutorVista;
}

export interface EstadoResolutores {
  /** ¿El tipo está prendido? */
  activo: Record<string, boolean>;
  /** Alcance por tipo (default EMPRESA). */
  alcance: Record<string, AlcanceResolutorVista>;
  /** País de la fila existente: no se edita, se conserva para no pisarlo al guardar. */
  pais: Record<string, number | null>;
}

/**
 * Estado inicial del editor. Reglas:
 * - Una fila INACTIVA no prende nada (es la auditoría de algo que se apagó).
 * - Si para el mismo tipo hay una fila GLOBAL y una de empresa, manda **GLOBAL**: atiende todas las
 *   empresas, incluida esta, y mostrarlo como «esta empresa» invitaría a degradarlo sin querer.
 * - Un alcance ausente o desconocido cuenta como EMPRESA (fail-closed: lo menos alcance posible).
 */
export function estadoDesdeFilas(
  tipos: readonly string[],
  filas: readonly FilaResolutorVista[] | null | undefined
): EstadoResolutores {
  const estado: EstadoResolutores = { activo: {}, alcance: {}, pais: {} };
  for (const t of tipos) {
    estado.activo[t] = false;
    estado.alcance[t] = 'EMPRESA';
    estado.pais[t] = null;
  }

  for (const f of filas ?? []) {
    if (!f?.activo) continue;
    const tipo = f.tipo;
    const esGlobal = f.alcance === 'GLOBAL';
    if (estado.activo[tipo] && estado.alcance[tipo] === 'GLOBAL' && !esGlobal) continue;
    estado.activo[tipo] = true;
    estado.alcance[tipo] = esGlobal ? 'GLOBAL' : 'EMPRESA';
    estado.pais[tipo] = f.paisId ?? null;
  }

  return estado;
}

/** Chips de «qué atiende hoy» (solo lectura, modo usuario). Una entrada por tipo activo. */
export function resumenAtiende(
  filas: readonly FilaResolutorVista[] | null | undefined,
  nombreEmpresa: string | null,
  labelDeTipo: (tipo: string) => string
): { tipo: string; label: string; etiqueta: string }[] {
  const vistos = new Set<string>();
  const salida: { tipo: string; label: string; etiqueta: string }[] = [];
  for (const f of filas ?? []) {
    if (!f?.activo || vistos.has(f.tipo)) continue;
    vistos.add(f.tipo);
    salida.push({
      tipo: f.tipo,
      label: labelDeTipo(f.tipo),
      etiqueta: f.alcance === 'GLOBAL' ? 'todas las empresas' : (nombreEmpresa ?? 'esta empresa'),
    });
  }
  return salida;
}
