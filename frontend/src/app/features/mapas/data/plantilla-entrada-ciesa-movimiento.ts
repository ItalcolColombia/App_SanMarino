/**
 * Definición de la página de movimiento para la plantilla "Entrada CIESA".
 * Afiliada al mapa con encabezado Entrada CIESA: campos que debe generar el paso de movimientos.
 */
export interface CampoMovimientoPlantilla {
  codigo: string;
  nombreDescriptivo: string;
  tipoDato: 'Numérico' | 'Alfanumérico';
  descripcion: string;
  default?: string;
  obligatorio?: boolean;
}

export const MOVIMIENTO_ENTRADA_CIESA: CampoMovimientoPlantilla[] = [
  { codigo: 'P_numero_registro', nombreDescriptivo: 'Número de registro', tipoDato: 'Numérico', descripcion: 'Num. consecutivo' },
  { codigo: 'P_tipo_registro', nombreDescriptivo: 'Tipo de registro', tipoDato: 'Numérico', descripcion: 'Valor fijo = 471', default: '471' },
  { codigo: 'P_subtipo_registro', nombreDescriptivo: 'Subtipo de registro', tipoDato: 'Numérico', descripcion: 'Valor fijo = 20', default: '20' },
  { codigo: 'P_version_tipo_registro', nombreDescriptivo: 'Versión del tipo de registro', tipoDato: 'Numérico', descripcion: 'Version = 02', default: '02' },
  { codigo: 'P_compania', nombreDescriptivo: 'Compañía', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de la compañía a la cual pertenece la información del registro' },
  { codigo: 'P_centro_operacion', nombreDescriptivo: 'Centro de operación', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código del centro de operación del documento' },
  { codigo: 'P_tipo_documento', nombreDescriptivo: 'Tipo de documento', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código del tipo de documento' },
  { codigo: 'P_numero_documento', nombreDescriptivo: 'Número de documento', tipoDato: 'Numérico', descripcion: 'Número de documento' },
  { codigo: 'P_numero_registro_mov', nombreDescriptivo: 'Número de registro (movimiento)', tipoDato: 'Numérico', descripcion: 'Número de registro del movimiento' },
  { codigo: 'P_campo', nombreDescriptivo: 'Campo', tipoDato: 'Alfanumérico', descripcion: 'Llave de cabecera' },
  { codigo: 'P_bodega', nombreDescriptivo: 'Bodega', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de bodega. Obligatorio, debe ser igual a U_BODEGA_CALIDAD', obligatorio: true },
  { codigo: 'P_ubicacion', nombreDescriptivo: 'Ubicación', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de ubicación. No obligatorio' },
  { codigo: 'P_lote', nombreDescriptivo: 'Lote', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código del lote. No obligatorio al ser entrada/salida o inventario' },
  { codigo: 'P_concepto', nombreDescriptivo: 'Concepto', tipoDato: 'Numérico', descripcion: '001=Gastos, 002=Transferencias, 003=Entradas, 004=Salidas, 005=Produccion, 006=Ajustes, 007=Resultados, 008=Cierre' },
  { codigo: 'P_motivo', nombreDescriptivo: 'Motivo', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de motivo. 44=Salida, 07=Ajuste' },
  { codigo: 'P_centro_negocio', nombreDescriptivo: 'Centro de negocio', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de centro de negocio. No obligatorio' },
  { codigo: 'P_unidad_medida', nombreDescriptivo: 'Unidad de medida', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de unidad de medida. No obligatorio' },
  { codigo: 'P_proyecto', nombreDescriptivo: 'Proyecto', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de proyecto. No obligatorio' },
  { codigo: 'P_cantidad_base', nombreDescriptivo: 'Cantidad base', tipoDato: 'Numérico', descripcion: 'Valor en captura, formato 15 decimales. Obligatorio si concepto es 1,2,3,4 o 7', obligatorio: true },
  { codigo: 'P_cantidad_adicional', nombreDescriptivo: 'Cantidad adicional', tipoDato: 'Numérico', descripcion: 'Valor en captura, formato 15 decimales. Obligatorio al ser entrada o inventario', obligatorio: true },
  { codigo: 'P_costo_promedio', nombreDescriptivo: 'Costo promedio', tipoDato: 'Numérico', descripcion: 'Valor en captura, formato 15 decimales. Obligatorio si concepto es 1,2,3,4 o 7' },
  { codigo: 'P_tipo_moneda', nombreDescriptivo: 'Tipo de moneda', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de tipo de moneda. No obligatorio' },
  { codigo: 'P_descripcion', nombreDescriptivo: 'Descripción', tipoDato: 'Alfanumérico', descripcion: 'Descripción del movimiento' },
  { codigo: 'P_subcuenta_item', nombreDescriptivo: 'Subcuenta de ítem', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, subcuenta del ítem. No obligatorio' },
  { codigo: 'P_unidad_medida_venta_item', nombreDescriptivo: 'Unidad de medida de venta del ítem', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, unidad de medida de venta. No obligatorio' },
  { codigo: 'P_lote_entrada', nombreDescriptivo: 'Lote entrada', tipoDato: 'Alfanumérico', descripcion: 'Valor en maestro, código de lote. No obligatorio' },
  { codigo: 'P_referencia', nombreDescriptivo: 'Referencia', tipoDato: 'Alfanumérico', descripcion: 'No obligatorio, va referencia de códigos de barras' },
  { codigo: 'P_codigo_barras', nombreDescriptivo: 'Código de barras', tipoDato: 'Alfanumérico', descripcion: 'No obligatorio, va código de barras' },
  { codigo: 'P_extension_1', nombreDescriptivo: 'Extensión 1', tipoDato: 'Alfanumérico', descripcion: 'No obligatorio, va formato extensión 1' },
  { codigo: 'P_extension_2', nombreDescriptivo: 'Extensión 2', tipoDato: 'Alfanumérico', descripcion: 'No obligatorio, va formato extensión 2' }
];
