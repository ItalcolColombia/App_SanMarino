# Detalle de la mejora: Logo de empresas y logo principal

**Buenas tardes.**

Se implementa la nueva funcionalidad para **modificar logo de empresas** y **cambio de logo principal** de la aplicación. A continuación se detalla lo realizado.

---

## 1. Logo por empresa (gestión en Configuración)

### Base de datos
- En la tabla **`companies`** se agregaron las columnas:
  - **`logo_bytes`** (BYTEA): imagen del logo en bytes.
  - **`logo_content_type`** (VARCHAR): tipo MIME (p. ej. `image/png`, `image/jpeg`).
- Script de migración: `backend/sql/add_company_logo.sql`.

### Backend
- Entidad **Company**: propiedades `LogoBytes` y `LogoContentType`.
- DTOs y servicios actualizados para enviar/recibir el logo (como Base64 o Data URL según el contrato del API).
- Endpoints de creación y actualización de empresa aceptan el logo; se persiste en BD y se devuelve en las respuestas (p. ej. `logoDataUrl` o equivalente) para uso en el frontend.

### Frontend – Gestión de empresas (Config → Gestión de empresas)
- **Listado:** columna **Logo** en la tabla de empresas; se muestra la imagen si la empresa tiene logo, o "—" si no.
- **Modal crear/editar empresa:**
  - Campo **"Logo de la empresa"** con:
    - Vista previa del logo actual o del archivo seleccionado.
    - Selector de archivo (imagen PNG/JPG).
    - Botón **"Quitar logo"** para eliminar el logo de la empresa.
  - Validaciones: solo imágenes PNG/JPG; tamaño máximo 512 KB.
  - Al guardar, se envía el logo al backend; si se edita la **empresa activa**, el logo del menú se actualiza de inmediato (sin necesidad de cerrar sesión).

### Uso del logo de la empresa en la aplicación
- En el **sidebar (menú lateral)**:
  - Se muestra siempre el **logo de marca** (Intalfoods Zootécnico).
  - Si la empresa activa tiene logo cargado, se muestra además el **logo de la empresa** junto al de marca.
- El logo activo se obtiene de la sesión (`activeCompanyLogoDataUrl`); al cambiar o actualizar el logo de la empresa activa en Gestión de empresas, se actualiza en storage y el sidebar refleja el cambio al instante.

---

## 2. Logo principal de la aplicación

### Favicon (icono de la pestaña y de la URL)
- El **favicon** de la aplicación pasa a ser el **logo de Intalfoods Zootécnico**.
  - Archivo utilizado: `frontend/src/assets/brand/logo_intalfoods_zootenico.png`.
  - Configuración en `frontend/src/index.html`:
    - `rel="icon"` para el icono en la pestaña del navegador y en la barra de direcciones.
    - `rel="apple-touch-icon"` para el icono al agregar la aplicación a la pantalla de inicio en dispositivos iOS.

### Logo de marca en el menú
- En el **header del sidebar** se muestra el logo principal de la aplicación (**Intalfoods Zootécnico**) desde `assets/brand/logo_intalfoods_zootenico.png`, y opcionalmente el logo de la empresa activa cuando esté configurado.

---

## 3. Resumen

| Aspecto | Detalle |
|--------|---------|
| **Logo por empresa** | Alta/edición/eliminación en Config → Gestión de empresas; almacenamiento en BD; visualización en listado y en el sidebar cuando es la empresa activa. |
| **Actualización en vivo** | Al guardar cambios en el logo de la empresa activa, el menú actualiza el logo sin cerrar sesión. |
| **Logo principal (favicon)** | Icono de pestaña y URL = logo Intalfoods Zootécnico. |
| **Logo principal (menú)** | Header del sidebar = logo Intalfoods Zootécnico + logo de empresa (si existe). |

Si necesitan ampliar algún punto técnico o de negocio, se puede detallar en un anexo o en la documentación interna del proyecto.
