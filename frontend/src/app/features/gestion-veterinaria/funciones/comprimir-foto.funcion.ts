import { TareaCampoEvidenciaInput } from '../models/gestion-veterinaria.models';

const MAX_LADO = 1600;
const CALIDAD = 0.78;

/** Comprime una foto local antes de enviarla; no toca estado ni servicios Angular. */
export async function comprimirFoto(file: File): Promise<TareaCampoEvidenciaInput> {
  if (!file.type.startsWith('image/')) throw new Error('Seleccioná un archivo de imagen.');
  const bitmap = await createImageBitmap(file);
  const escala = Math.min(1, MAX_LADO / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.round(bitmap.width * escala));
  canvas.height = Math.max(1, Math.round(bitmap.height * escala));
  const context = canvas.getContext('2d');
  if (!context) throw new Error('El dispositivo no pudo preparar la fotografía.');
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  const blob = await new Promise<Blob>((resolve, reject) =>
    canvas.toBlob((value) => value ? resolve(value) : reject(new Error('No se pudo comprimir la fotografía.')),
      'image/jpeg', CALIDAD)
  );
  const base64 = await blobToDataUrl(blob);
  const baseName = file.name.replace(/\.[^.]+$/, '').slice(0, 160) || 'evidencia';
  return { base64, fileName: `${baseName}.jpg`, contentType: 'image/jpeg', sizeBytes: blob.size };
}

function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
}
