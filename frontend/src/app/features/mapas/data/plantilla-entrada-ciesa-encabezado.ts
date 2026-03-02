/**
 * Definición del encabezado para la plantilla "Entrada CIESA".
 * Corresponde al registro de tipo Encabezado del documento (campos técnicos, descripción, tipo de dato, etc.).
 */
export interface CampoEncabezadoPlantilla {
  codigo: string;
  nombreDescriptivo: string;
  tipoDato: 'Numérico' | 'Alfanumérico';
  descripcion: string;
  default?: string;
}

export const ENCABEZADO_ENTRADA_CIESA: CampoEncabezadoPlantilla[] = [
  { codigo: 'F_NUMERO_REG', nombreDescriptivo: 'Número de registro', tipoDato: 'Numérico', descripcion: 'Numérico consecutivo', default: '400' },
  { codigo: 'F_TIPO_REG', nombreDescriptivo: 'Tipo de documento', tipoDato: 'Numérico', descripcion: 'Tipo de registro', default: '50' },
  { codigo: 'F_CANT_REG', nombreDescriptivo: 'Cantidad de registros', tipoDato: 'Numérico', descripcion: 'Cantidad total de registros', default: '92' },
  { codigo: 'F_VERSION_REG', nombreDescriptivo: 'Versión del registro', tipoDato: 'Numérico', descripcion: 'Versión', default: '2' },
  { codigo: 'F_CIA', nombreDescriptivo: 'Compañía', tipoDato: 'Numérico', descripcion: 'Número de compañía a la cual hace referencia la información del registro' },
  { codigo: 'F_CONSEC_AUT_DOC', nombreDescriptivo: 'Consecutivo autorizado documento', tipoDato: 'Numérico', descripcion: 'Consecutivo del documento' },
  { codigo: 'TIPO_id_tipo', nombreDescriptivo: 'Tipo de documento', tipoDato: 'Alfanumérico', descripcion: 'Identificador del tipo de documento' },
  { codigo: 'TIPO_consec_tido', nombreDescriptivo: 'Consecutivo tipo documento', tipoDato: 'Numérico', descripcion: 'Consecutivo del tipo de documento' },
  { codigo: 'TIPO_fecha', nombreDescriptivo: 'Fecha documento', tipoDato: 'Alfanumérico', descripcion: 'Fecha del documento' },
  { codigo: 'TIPO_id_terminal', nombreDescriptivo: 'Terminal', tipoDato: 'Alfanumérico', descripcion: 'Identificador de terminal' },
  { codigo: 'TIPO_id_clase', nombreDescriptivo: 'Clase de documento', tipoDato: 'Alfanumérico', descripcion: 'Clase del documento' },
  { codigo: 'TIPO_id_estado_tido', nombreDescriptivo: 'Estado documento', tipoDato: 'Alfanumérico', descripcion: 'Estado del documento' },
  { codigo: 'TIPO_id_estado_tido_impresion', nombreDescriptivo: 'Estado impresión documento', tipoDato: 'Alfanumérico', descripcion: 'Estado de impresión' },
  { codigo: 'TIPO_notas', nombreDescriptivo: 'Notas tipo', tipoDato: 'Alfanumérico', descripcion: 'Notas del documento' },
  { codigo: 'TIPO_id_consec_origen', nombreDescriptivo: 'Concepto origen', tipoDato: 'Alfanumérico', descripcion: 'Consecutivo origen' },
  { codigo: 'TIPO_id_bodega_origen', nombreDescriptivo: 'Bodega origen', tipoDato: 'Alfanumérico', descripcion: 'Identificador bodega de origen' },
  { codigo: 'TIPO_id_bodega_destino', nombreDescriptivo: 'Bodega destino', tipoDato: 'Alfanumérico', descripcion: 'Identificador bodega de destino' },
  { codigo: 'ENTRAD_id_co', nombreDescriptivo: 'Documento externo', tipoDato: 'Alfanumérico', descripcion: 'Identificador documento externo' },
  { codigo: 'ENTRAD_id_tipo', nombreDescriptivo: 'Centro de operación', tipoDato: 'Alfanumérico', descripcion: 'Tipo de entrada / centro de operación' },
  { codigo: 'ENTRAD_consec', nombreDescriptivo: 'Consecutivo entrada', tipoDato: 'Numérico', descripcion: 'Consecutivo de la entrada' },
  { codigo: 'ENTRAD_id_vehiculo', nombreDescriptivo: 'Código vehículo', tipoDato: 'Alfanumérico', descripcion: 'Código del vehículo' },
  { codigo: 'ENTRAD_id_transportador', nombreDescriptivo: 'Código transportador', tipoDato: 'Alfanumérico', descripcion: 'Código del transportador' },
  { codigo: 'ENTRAD_id_conductor_real_transp', nombreDescriptivo: 'Conductor real transportador', tipoDato: 'Alfanumérico', descripcion: 'Código conductor real del transportador' },
  { codigo: 'ENTRAD_id_conductor_mandante', nombreDescriptivo: 'Conductor mandante', tipoDato: 'Alfanumérico', descripcion: 'Código conductor del mandante' },
  { codigo: 'ENTRAD_nombre_conductor_mandante', nombreDescriptivo: 'Nombre conductor mandante', tipoDato: 'Alfanumérico', descripcion: 'Nombre del conductor mandante' },
  { codigo: 'ENTRAD_identif_conductor_mandante', nombreDescriptivo: 'Identificación conductor mandante', tipoDato: 'Alfanumérico', descripcion: 'Identificación del conductor' },
  { codigo: 'ENTRAD_numero_guia', nombreDescriptivo: 'Número de guía', tipoDato: 'Alfanumérico', descripcion: 'Número de la guía' },
  { codigo: 'ENTRAD_placa', nombreDescriptivo: 'Placa', tipoDato: 'Alfanumérico', descripcion: 'Placa del vehículo' },
  { codigo: 'ENTRAD_peso', nombreDescriptivo: 'Peso', tipoDato: 'Numérico', descripcion: 'Peso' },
  { codigo: 'ENTRAD_volumen', nombreDescriptivo: 'Volumen', tipoDato: 'Numérico', descripcion: 'Volumen' },
  { codigo: 'ENTRAD_valor_mer_guia', nombreDescriptivo: 'Valor mercancía guía', tipoDato: 'Numérico', descripcion: 'Valor de la mercancía en la guía' },
  { codigo: 'ENTRAD_notas', nombreDescriptivo: 'Notas entrada', tipoDato: 'Alfanumérico', descripcion: 'Notas de la entrada' }
];
