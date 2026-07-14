/**
 * Bloqueo de lotes cerrados / corridas anteriores en "Venta por granja".
 *
 * Dentro de un mismo galpón puede haber varias corridas históricas (ej. lotes `2601`, `2602`,
 * `2603`). Solo la corrida vigente (la de `fechaEncaset` más reciente) debe admitir cantidades en
 * la venta por granja; las demás — y cualquier lote ya `Cerrado` — quedan bloqueadas salvo que el
 * usuario tenga el permiso `movimientos_pollo_engorde.vender_lotes_cerrados`. Función pura sin
 * estado de Angular.
 */
import { VentaLineaGranja } from '../models/venta-granja.model';
import { LoteAveEngordeDto } from '../../lote-engorde/services/lote-engorde.service';

function esCerrado(estado: string | null | undefined): boolean {
  return (estado ?? '').trim().toLowerCase() === 'cerrado';
}

function fechaEncasetOrden(lote: LoteAveEngordeDto | undefined): number {
  const t = lote?.fechaEncaset ? new Date(lote.fechaEncaset).getTime() : NaN;
  return isNaN(t) ? -Infinity : t;
}

/** Determina, por galpón, el `loteId` de la corrida vigente (fechaEncaset más reciente; desempate por loteId mayor). */
function calcularVigentePorGalpon(
  lineas: VentaLineaGranja[],
  lotesPorId: Map<number, LoteAveEngordeDto>
): Map<string, number> {
  const porGalpon = new Map<string, VentaLineaGranja[]>();
  for (const linea of lineas) {
    if (!porGalpon.has(linea.galponId)) porGalpon.set(linea.galponId, []);
    porGalpon.get(linea.galponId)!.push(linea);
  }

  const vigentePorGalpon = new Map<string, number>();
  for (const [galponId, lineasGalpon] of porGalpon) {
    let vigente = lineasGalpon[0];
    for (const linea of lineasGalpon.slice(1)) {
      const fechaActual = fechaEncasetOrden(lotesPorId.get(linea.loteId));
      const fechaVigente = fechaEncasetOrden(lotesPorId.get(vigente.loteId));
      const esMasReciente =
        fechaActual > fechaVigente || (fechaActual === fechaVigente && linea.loteId > vigente.loteId);
      if (esMasReciente) vigente = linea;
    }
    vigentePorGalpon.set(galponId, vigente.loteId);
  }
  return vigentePorGalpon;
}

/**
 * Marca cada línea con `bloqueada`/`motivoBloqueo` según la regla de negocio (lote cerrado o
 * corrida anterior en el mismo galpón). No muta las líneas de entrada.
 */
export function marcarLotesBloqueadosVenta(
  lineas: VentaLineaGranja[],
  lotes: LoteAveEngordeDto[]
): VentaLineaGranja[] {
  const lotesPorId = new Map(lotes.map((l) => [l.loteAveEngordeId, l]));
  const vigentePorGalpon = calcularVigentePorGalpon(lineas, lotesPorId);

  return lineas.map((linea) => {
    const lote = lotesPorId.get(linea.loteId);
    const cerrado = esCerrado(lote?.estadoOperativoLote);
    const esVigente = vigentePorGalpon.get(linea.galponId) === linea.loteId;
    const bloqueada = cerrado || !esVigente;
    return {
      ...linea,
      bloqueada,
      motivoBloqueo: !bloqueada ? undefined : cerrado ? 'Lote cerrado' : 'Corrida anterior en este galpón'
    };
  });
}
