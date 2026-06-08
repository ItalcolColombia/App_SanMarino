import { LogoValidationResult } from '../models/company-management.model';

const MAX_LOGO_BYTES = 512 * 1024;

export function readLogoFile(file: File): Promise<LogoValidationResult> {
  if (!file.type?.startsWith('image/')) {
    return Promise.resolve({ dataUrl: null, error: 'Seleccione un archivo de imagen (PNG/JPG).' });
  }
  if (file.size > MAX_LOGO_BYTES) {
    return Promise.resolve({ dataUrl: null, error: 'El logo supera 512 KB. Use una imagen más liviana.' });
  }

  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      resolve({ dataUrl: typeof result === 'string' ? result : null, error: null });
    };
    reader.onerror = () => resolve({ dataUrl: null, error: 'No se pudo leer la imagen.' });
    reader.readAsDataURL(file);
  });
}
