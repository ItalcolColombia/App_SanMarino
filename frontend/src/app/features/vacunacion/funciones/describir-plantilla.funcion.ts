/**
 * Alcance y vigencia de una plantilla, en una línea de texto.
 *
 * PURA: sin `this`, sin DI, sin servicios. Recibe la plantilla y —cuando hace falta— el "hoy" por
 * parámetro, nunca `new Date()` adentro: así el resultado es reproducible y testeable.
 *
 * Existe porque «Levante · null · 2026-07-01» no le dice nada a nadie en una tabla, y la alternativa
 * (armar el texto en el template con tres `ngIf` anidados) se duplica en cada pantalla que lo necesite.
 */
import { LINEA_PRODUCTIVA_LABEL } from '../models/vacunacion.model';
import { VacunacionPlantillaDto, VacunacionPlantillaDetalleDto } from '../models/vacunacion-plantilla.model';

type PlantillaLike = Pick<VacunacionPlantillaDto, 'lineaProductiva' | 'raza' | 'vigenteDesde' | 'activa'>;

/** "Levante (Postura) · Ross 308" o "… · todas las razas" cuando es comodín. */
export function describirAlcance(p: PlantillaLike): string {
  const linea = LINEA_PRODUCTIVA_LABEL[p.lineaProductiva] ?? p.lineaProductiva;
  const raza = (p.raza ?? '').trim();
  return `${linea} · ${raza.length ? raza : 'todas las razas'}`;
}

/** "Desde el 01/07/2026" o "Sin fecha de vigencia" (aplica a cualquier lote de la línea). */
export function describirVigencia(p: PlantillaLike): string {
  const desde = (p.vigenteDesde ?? '').slice(0, 10);
  if (!desde) return 'Sin fecha de vigencia';
  const [a, m, d] = desde.split('-');
  return `Desde el ${d}/${m}/${a}`;
}

/** "Semana 3" / "Día 12" — el objetivo tal como se va a materializar. */
export function describirObjetivo(unidadObjetivo: string, valorObjetivo: number): string {
  return unidadObjetivo === 'Dia' ? `Día ${valorObjetivo}` : `Semana ${valorObjetivo}`;
}

/** "−2 / +6 días" — el ancho de la franja válida alrededor del objetivo. */
export function describirFranja(rangoDiasAntes: number, rangoDiasDespues: number): string {
  return `−${rangoDiasAntes} / +${rangoDiasDespues} días`;
}

/**
 * Advertencia para una plantilla que está cargada pero **no va a hacer nada todavía**, o `null` si
 * no hay nada que advertir. Sin esto, una plantilla apagada o vacía se ve igual que una lista para
 * usarse, y el usuario se entera recién cuando el lote no recibe cronograma.
 */
export function advertenciaPlantilla(p: PlantillaLike & { cantidadItems: number }): string | null {
  if (!p.activa) return 'Está apagada: ningún lote la va a tomar.';
  if (p.cantidadItems === 0) return 'No tiene vacunas cargadas todavía.';
  return null;
}

/**
 * Ordena los ítems como se van a materializar: primero el orden que puso el usuario, después el
 * objetivo y por último el id. Devuelve **un arreglo nuevo** — no muta la entrada — y es determinista:
 * dos ítems empatados no cambian de lugar según cómo los devolvió la API.
 */
export function ordenarItemsPlantilla(
  items: VacunacionPlantillaDetalleDto['items']
): VacunacionPlantillaDetalleDto['items'] {
  return [...items].sort(
    (a, b) => a.orden - b.orden || a.valorObjetivo - b.valorObjetivo || a.id - b.id
  );
}
