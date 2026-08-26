// src/app/features/config/guia-genetica-santa-reyes/funciones/validar-formulario-guia.funcion.ts
/**
 * Validación del formulario de alta / edición y armado del DTO. Funciones **puras**: sin `this`,
 * sin DI, sin toast.
 *
 * Espeja `GuiaGeneticaSantaReyesService.ValidarClaveNatural` del backend (raza y año obligatorios,
 * semana > 0). No la reemplaza: el backend sigue siendo el que manda —acá sólo se evita el viaje.
 */
import {
  CreateGuiaGeneticaSantaReyesDto,
  FormularioGuiaGeneticaSantaReyes,
  MAX_LARGO_ANIO_GUIA,
  MAX_LARGO_RAZA,
  SEMANA_COBERTURA_MAX,
  SEMANA_COBERTURA_MIN,
  UpdateGuiaGeneticaSantaReyesDto
} from '../models/guia-genetica-santa-reyes.model';

/** Resultado de validar el formulario. */
export interface ResultadoValidacionGuia {
  /** `true` si no hay errores. Las advertencias **no** bloquean. */
  valido: boolean;
  /** Impiden guardar. */
  errores: string[];
  /** Se muestran pero dejan guardar (p. ej. una semana fuera de 18–140). */
  advertencias: string[];
}

/** Formulario vacío (alta). */
export function formularioGuiaVacio(): FormularioGuiaGeneticaSantaReyes {
  return { id: null, raza: '', anioGuia: '', edad: '', prodPorcentaje: '', retiroAcH: '', grAveDiaH: '' };
}

/** DTO ⇒ formulario (edición). Las métricas nulas quedan **vacías**, nunca en `0`. */
export function formularioDesdeDto(dto: {
  id: number;
  raza: string;
  anioGuia: string;
  edad: number;
  prodPorcentaje: number | null;
  retiroAcH: number | null;
  grAveDiaH: number | null;
}): FormularioGuiaGeneticaSantaReyes {
  return {
    id: dto.id,
    raza: dto.raza ?? '',
    anioGuia: dto.anioGuia ?? '',
    edad: dto.edad != null ? String(dto.edad) : '',
    prodPorcentaje: dto.prodPorcentaje != null ? String(dto.prodPorcentaje) : '',
    retiroAcH: dto.retiroAcH != null ? String(dto.retiroAcH) : '',
    grAveDiaH: dto.grAveDiaH != null ? String(dto.grAveDiaH) : ''
  };
}

/**
 * Métrica opcional: vacío ⇒ `null` (**no** 0). El usuario que deja la celda en blanco está
 * diciendo «esta semana no tiene dato», que es distinto de «esta semana vale cero».
 * Acepta coma o punto decimal: en un teclado en español la coma es lo que sale.
 */
export function metricaOpcional(texto: string | null | undefined): number | null {
  const limpio = (texto ?? '').trim().replace(',', '.');
  if (!limpio) return null;
  const n = Number(limpio);
  return Number.isFinite(n) ? n : null;
}

/** `true` si el texto tiene algo escrito que **no** es un número (para distinguirlo de «vacío»). */
function esNumeroInvalido(texto: string | null | undefined): boolean {
  const limpio = (texto ?? '').trim().replace(',', '.');
  if (!limpio) return false;
  return !Number.isFinite(Number(limpio));
}

/** Valida el formulario. */
export function validarFormularioGuia(form: FormularioGuiaGeneticaSantaReyes): ResultadoValidacionGuia {
  const errores: string[] = [];
  const advertencias: string[] = [];

  const raza = (form.raza ?? '').trim();
  const anio = (form.anioGuia ?? '').trim();
  const edadTexto = (form.edad ?? '').trim();

  if (!raza) errores.push('La raza es obligatoria.');
  else if (raza.length > MAX_LARGO_RAZA) errores.push(`La raza no puede superar los ${MAX_LARGO_RAZA} caracteres.`);

  if (!anio) errores.push('El año de la guía es obligatorio.');
  else if (anio.length > MAX_LARGO_ANIO_GUIA) errores.push(`El año no puede superar los ${MAX_LARGO_ANIO_GUIA} caracteres.`);

  const edad = Number(edadTexto);
  if (!edadTexto) {
    errores.push('La semana (edad) es obligatoria.');
  } else if (!Number.isInteger(edad)) {
    errores.push('La semana (edad) debe ser un número entero.');
  } else if (edad <= 0) {
    errores.push('La semana (edad) debe ser mayor que cero.');
  } else if (edad < SEMANA_COBERTURA_MIN || edad > SEMANA_COBERTURA_MAX) {
    // Advertencia, NO error: la guía sembrada cubre 18–140, pero el modelo no lo prohíbe y una
    // línea nueva puede legítimamente empezar antes. Se avisa; no se bloquea.
    advertencias.push(
      `La semana ${edad} queda fuera del tramo que cubre esta guía (${SEMANA_COBERTURA_MIN}–${SEMANA_COBERTURA_MAX}).`
    );
  }

  if (esNumeroInvalido(form.prodPorcentaje)) errores.push('«% Producción» no es un número válido.');
  if (esNumeroInvalido(form.retiroAcH)) errores.push('«% Retiro acum. H» no es un número válido.');
  if (esNumeroInvalido(form.grAveDiaH)) errores.push('«gr/ave/día H» no es un número válido.');

  return { valido: errores.length === 0, errores, advertencias };
}

/** Formulario ⇒ DTO de alta. Asume que ya pasó `validarFormularioGuia`. */
export function construirCreateDtoGuia(
  form: FormularioGuiaGeneticaSantaReyes
): CreateGuiaGeneticaSantaReyesDto {
  return {
    raza: (form.raza ?? '').trim(),
    anioGuia: (form.anioGuia ?? '').trim(),
    edad: Number((form.edad ?? '').trim()),
    prodPorcentaje: metricaOpcional(form.prodPorcentaje),
    retiroAcH: metricaOpcional(form.retiroAcH),
    grAveDiaH: metricaOpcional(form.grAveDiaH)
  };
}

/**
 * Formulario ⇒ DTO de edición. El `id` va en el cuerpo **y** en la ruta: el controller rechaza con
 * 400 si no coinciden.
 */
export function construirUpdateDtoGuia(
  form: FormularioGuiaGeneticaSantaReyes,
  id: number
): UpdateGuiaGeneticaSantaReyesDto {
  return { id, ...construirCreateDtoGuia(form) };
}
