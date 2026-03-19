# Guía genética Ecuador

## Base de datos

- Script PostgreSQL: `backend/sql/create_guia_genetica_ecuador_tables.sql`
- Menú (opcional): `backend/sql/add_guia_genetica_ecuador_menu.sql` → ruta `/config/guia-genetica-ecuador`

## API (`/api/guia-genetica-ecuador`)

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `filters` | Listas `razas` y `anos` existentes (empresa actual). |
| GET | `sexos?raza=&anioGuia=` | Sexos con datos para guía **active** (mixto/hembra/macho). |
| GET | `datos?raza=&anioGuia=&sexo=` | Filas de detalle ordenadas por día. |
| POST | `import` | `multipart/form-data`: `file`, `raza`, `anioGuia`, `estado` (active/inactive). Hojas: **mixto**, **hembra**, **macho** (nombre case-insensitive). Si hay errores de validación, no se aplica nada (rollback). |
| POST | `manual` | JSON `GuiaGeneticaEcuadorManualRequestDto`: reemplaza todo el detalle del **sexo** indicado. |

## Excel

Encabezados reconocidos (tolerantes a mayúsculas, acentos y `%`): Día, Peso corporal, Ganancia diaria, Promedio ganancia diaria, Cantidad alimento diario, Alimento acumulado, CA, Mortalidad / selección.

## Frontend

- Pantalla: **Configuración** → `/config/guia-genetica-ecuador` (tras ejecutar script de menú y asignar al rol).

## Dominio

Corregido `using` en entidades: `AuditableEntity` vive en `ZooSanMarino.Domain.Entities` (no en `._Base`).
