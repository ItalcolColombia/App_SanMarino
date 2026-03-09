# Tema unificado Italfoods

Colores del logo: **naranja** (ital, cuadros) + **verde** (foods, borde) + **crema** (fondo).

- **Tablas**: fondos verdes, letras naranja.
- **Botones y modales**: mezcla coherente de verde y naranja.

## Uso en HTML (Tailwind 100%)

### Colores Tailwind
- `bg-ital-green`, `text-ital-orange`, `bg-ital-cream`
- Variantes: `-light`, `-dark`, `-50`, `-100` (ej: `bg-ital-green-100`, `text-ital-orange-dark`)

### Clases del tema (theme-italfoods.scss)

| Clase | Uso |
|-------|-----|
| `table-italfoods` | Tabla: cabecera verde, celdas alternas crema, texto naranja |
| `table-italfoods--cream` | Variante: cabecera verde oscuro, cuerpo crema |
| `btn-italfoods-primary` | Botón verde (primario) |
| `btn-italfoods-secondary` | Botón borde naranja (secundario) |
| `btn-italfoods-orange` | Botón naranja sólido |
| `modal-italfoods` | Contenedor modal: borde verde, header verde |
| `modal-italfoods__header` | Cabecera del modal |
| `modal-italfoods__body` | Cuerpo |
| `modal-italfoods__title` | Título (naranja) |
| `modal-italfoods__footer` | Pie con acciones |
| `card-italfoods` | Card con borde verde suave |
| `card-italfoods__title` | Título de card (naranja) |
| `input-italfoods` | Input con borde verde y focus naranja |
| `text-ital-orange` / `text-ital-orange-dark` | Texto acento naranja |
| `bg-ital-green-soft` / `bg-ital-orange-soft` | Fondos suaves |

### Variables CSS (cualquier componente)
```scss
var(--ital-orange)
var(--ital-green)
var(--ital-cream)
var(--ital-orange-dark)
var(--ital-green-100)
// etc.
```

## Migrar un módulo
En el `.scss` del componente puedes sustituir colores locales por clases Tailwind (`text-ital-orange`, `bg-ital-green`) o por las clases del tema (`table-italfoods`, `btn-italfoods-primary`). Las variables `--ital-*` en `:root` están disponibles en toda la app.
