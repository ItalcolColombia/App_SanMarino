/**
 * Valida los 4 insumos ENTEROS de la liquidación Panamá antes de enviarlos al backend.
 *
 * Por qué existe: `DiasEnGranja`/`DiasEngorde`/`AvesFinalGranja`/`AvesBeneficiada` son `int` en el
 * contrato (`GuardarLiquidacionPanamaRequest`), pero sus 4 inputs numéricos del modal no restringían
 * decimales — solo exigían `> 0` (`panamaCamposCompletos`). Un decimal tipeado ahí (ej. pegar
 * "24046.5" desde un reporte) pasaba ese gate y el backend lo rechazaba en la deserialización del
 * JSON, ANTES de llegar a ninguna regla de negocio — un 400 sin mensaje utilizable para el usuario
 * (caso real: liquidación Panamá, lote 13-1, 26-ago-2026).
 */
export interface InsumosEnterosPanamaLike {
  diasEnGranja: number | null;
  diasEngorde: number | null;
  avesFinalGranja: number | null;
  avesBeneficiada: number | null;
}

const CAMPOS_ENTEROS: ReadonlyArray<{ clave: keyof InsumosEnterosPanamaLike; etiqueta: string }> = [
  { clave: 'diasEnGranja', etiqueta: 'Días en Granja' },
  { clave: 'diasEngorde', etiqueta: 'Días de Engorde' },
  { clave: 'avesFinalGranja', etiqueta: 'Aves Finales en Granja' },
  { clave: 'avesBeneficiada', etiqueta: 'Aves Beneficiadas' }
];

/** `null` si los 4 campos enteros son válidos; si no, el mensaje a mostrar (rojo) al usuario. */
export function validarInsumosEnterosPanama(insumos: InsumosEnterosPanamaLike): string | null {
  for (const { clave, etiqueta } of CAMPOS_ENTEROS) {
    const valor = insumos[clave];
    if (valor != null && !Number.isInteger(valor)) {
      return `"${etiqueta}" debe ser un número entero, sin decimales (se recibió ${valor}).`;
    }
  }
  return null;
}
