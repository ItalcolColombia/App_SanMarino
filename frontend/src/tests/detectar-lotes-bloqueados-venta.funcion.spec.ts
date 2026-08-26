/**
 * Venta por granja — qué línea bloquea, y cuál de esos bloqueos destraba el permiso.
 *
 * El defecto que motiva estas pruebas: el front prometía que
 * `movimientos_pollo_engorde.vender_lotes_cerrados` habilitaba vender un lote **cerrado**. No es
 * cierto — el backend rechaza toda escritura sobre un lote liquidado en
 * `LiquidacionCongeladaGateCalculos.ValidarEscritura` (→ 400) y no consulta ese permiso en ninguna
 * parte. El usuario cargaba las cantidades y el guardado le rebotaba sin explicación.
 *
 * `bypassablePorPermiso` es la separación: **sí** para la corrida anterior (el backend no tiene
 * noción de «corrida vigente» y la acepta), **no** para el lote cerrado.
 */
import { marcarLotesBloqueadosVenta } from '../app/features/movimientos-pollo-engorde/funciones/detectar-lotes-bloqueados-venta.funcion';
import { VentaLineaGranja } from '../app/features/movimientos-pollo-engorde/models/venta-granja.model';
import { LoteAveEngordeDto } from '../app/features/lote-engorde/services/lote-engorde.service';

function linea(loteId: number, galponId: string): VentaLineaGranja {
  return { loteId, galponId } as VentaLineaGranja;
}

function lote(id: number, fechaEncaset: string, estado: string): LoteAveEngordeDto {
  return {
    loteAveEngordeId: id,
    fechaEncaset,
    estadoOperativoLote: estado
  } as LoteAveEngordeDto;
}

describe('marcarLotesBloqueadosVenta · qué se bloquea', () => {
  it('la corrida vigente y abierta no se bloquea', () => {
    const [r] = marcarLotesBloqueadosVenta(
      [linea(10, 'G1')],
      [lote(10, '2026-08-01', 'Abierto')]
    );
    expect(r.bloqueada).toBeFalse();
    expect(r.motivoBloqueo).toBeUndefined();
    expect(r.bypassablePorPermiso).toBeFalse();
  });

  it('la corrida anterior del mismo galpón se bloquea, y el permiso SÍ la destraba', () => {
    const res = marcarLotesBloqueadosVenta(
      [linea(10, 'G1'), linea(11, 'G1')],
      [lote(10, '2026-06-01', 'Abierto'), lote(11, '2026-08-01', 'Abierto')]
    );
    const anterior = res.find((l) => l.loteId === 10)!;
    const vigente = res.find((l) => l.loteId === 11)!;

    expect(anterior.bloqueada).toBeTrue();
    expect(anterior.motivoBloqueo).toBe('Corrida anterior en este galpón');
    expect(anterior.bypassablePorPermiso).toBeTrue();

    expect(vigente.bloqueada).toBeFalse();
  });

  it('el lote cerrado se bloquea y NINGÚN permiso lo destraba', () => {
    const [r] = marcarLotesBloqueadosVenta(
      [linea(10, 'G1')],
      [lote(10, '2026-08-01', 'Cerrado')]
    );
    expect(r.bloqueada).toBeTrue();
    expect(r.motivoBloqueo).toBe('Lote cerrado');
    expect(r.bypassablePorPermiso).toBeFalse();
  });

  it('cerrado gana sobre corrida anterior: si es las dos cosas, sigue sin ser bypassable', () => {
    const res = marcarLotesBloqueadosVenta(
      [linea(10, 'G1'), linea(11, 'G1')],
      [lote(10, '2026-06-01', 'Cerrado'), lote(11, '2026-08-01', 'Abierto')]
    );
    const cerradoYAnterior = res.find((l) => l.loteId === 10)!;

    expect(cerradoYAnterior.bloqueada).toBeTrue();
    expect(cerradoYAnterior.motivoBloqueo).toBe('Lote cerrado');
    // Es corrida anterior Y está cerrado. El gate del backend lo rechaza igual ⇒ no bypassable.
    expect(cerradoYAnterior.bypassablePorPermiso).toBeFalse();
  });

  it('reconoce el estado cerrado sin importar mayúsculas ni espacios', () => {
    for (const estado of ['cerrado', 'CERRADO', '  Cerrado  ']) {
      const [r] = marcarLotesBloqueadosVenta([linea(10, 'G1')], [lote(10, '2026-08-01', estado)]);
      expect(r.bloqueada).withContext(estado).toBeTrue();
      expect(r.bypassablePorPermiso).withContext(estado).toBeFalse();
    }
  });

  it('no muta las líneas de entrada', () => {
    const entrada = [linea(10, 'G1')];
    marcarLotesBloqueadosVenta(entrada, [lote(10, '2026-08-01', 'Cerrado')]);
    expect(entrada[0].bloqueada).toBeUndefined();
    expect(entrada[0].bypassablePorPermiso).toBeUndefined();
  });
});
