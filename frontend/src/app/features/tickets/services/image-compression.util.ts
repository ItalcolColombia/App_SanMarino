// src/app/features/tickets/services/image-compression.util.ts
//
// Optimización de Base64 para adjuntos de tickets.
// Estrategia: redimensionar + recomprimir en el cliente ANTES de codificar a Base64,
// reduciendo el peso 80-95 % y evitando payloads de varios MB que congelan la red/UI.
//
// - Usa OffscreenCanvas + createImageBitmap cuando están disponibles (más eficiente,
//   no toca el DOM); cae a HTMLCanvasElement + Image como fallback.
// - La conversión a Base64 se hace UNA sola vez, sobre el blob ya liviano.
// - Para previsualizar usar `URL.createObjectURL(blob)` (no data URLs) y revocarlo al soltar.

const DEFAULT_MAX_SIDE = 1600;
const DEFAULT_QUALITY = 0.7;
const OUTPUT_TYPE = 'image/webp';

export interface CompressResult {
  blob: Blob;
  contentType: string;
}

/** Redimensiona (lado largo <= maxSide) y recomprime la imagen a WebP/JPEG. */
export async function compressImage(
  file: File,
  maxSide: number = DEFAULT_MAX_SIDE,
  quality: number = DEFAULT_QUALITY,
): Promise<CompressResult> {
  const source = await decode(file);
  const { width, height } = source;

  const scale = Math.min(1, maxSide / Math.max(width, height));
  const w = Math.max(1, Math.round(width * scale));
  const h = Math.max(1, Math.round(height * scale));

  let blob: Blob;

  if (typeof OffscreenCanvas !== 'undefined') {
    const canvas = new OffscreenCanvas(w, h);
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('No se pudo obtener el contexto 2D (OffscreenCanvas).');
    ctx.drawImage(source.image as CanvasImageSource, 0, 0, w, h);
    blob = await canvas.convertToBlob({ type: OUTPUT_TYPE, quality });
  } else {
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('No se pudo obtener el contexto 2D (canvas).');
    ctx.drawImage(source.image as CanvasImageSource, 0, 0, w, h);
    blob = await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(
        b => (b ? resolve(b) : reject(new Error('canvas.toBlob devolvió null'))),
        OUTPUT_TYPE,
        quality,
      ));
  }

  source.release();
  // Si el navegador no soporta WebP, devolverá otro tipo (ej. image/png): respetarlo.
  return { blob, contentType: blob.type || OUTPUT_TYPE };
}

/** Convierte un Blob a data URL Base64 (formato `data:<tipo>;base64,...`). */
export function blobToBase64(blob: Blob): Promise<string> {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error ?? new Error('Error al leer el blob'));
    reader.readAsDataURL(blob);
  });
}

// ── Internos ──────────────────────────────────────────────────

interface DecodedSource {
  image: ImageBitmap | HTMLImageElement;
  width: number;
  height: number;
  release: () => void;
}

async function decode(file: File): Promise<DecodedSource> {
  // createImageBitmap es lo más rápido y libera memoria explícitamente.
  if (typeof createImageBitmap === 'function') {
    try {
      const bitmap = await createImageBitmap(file);
      return {
        image: bitmap,
        width: bitmap.width,
        height: bitmap.height,
        release: () => bitmap.close(),
      };
    } catch {
      // cae al fallback
    }
  }

  const url = URL.createObjectURL(file);
  try {
    const img = await loadImage(url);
    return {
      image: img,
      width: img.naturalWidth,
      height: img.naturalHeight,
      release: () => URL.revokeObjectURL(url),
    };
  } catch (err) {
    URL.revokeObjectURL(url);
    throw err;
  }
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('No se pudo decodificar la imagen'));
    img.src = url;
  });
}
