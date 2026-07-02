# engorde-comun — lógica compartida multi-país de pollo engorde

Código **común** a los módulos de engorde por país (`aves-engorde` = Colombia, `aves-engorde-panama` = Panamá). Antes cada país tenía su copia byte-idéntica; un fix en un país no llegaba al otro.

Convención:
- `models/` — tipos/interfaces compartidos.
- `services/` — servicios de cálculo puros (sin estado de país); las diferencias por país entran por **parámetros**, nunca por copia del archivo.
- Los módulos por país mantienen shims de re-export (`export * from '../../engorde-comun/...'`) para no romper imports existentes; el código nuevo debe importar directo de `engorde-comun`.
- Prohibido volver a copiar estos archivos a un módulo de país.
