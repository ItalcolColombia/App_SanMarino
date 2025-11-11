# 📂 SQL Scripts - San Marino

Este directorio contiene todos los scripts SQL del proyecto organizados por categoría.

## 📁 Estructura

```
sql/
├── README.md (este archivo)
├── conversion/          # Scripts de conversión de datos
├── verify/              # Scripts de verificación
├── backup/              # Backups de base de datos
└── *.sql                # Scripts generales y creación de tablas
```

---

## 📋 Categorías

### 🔄 conversion/
Scripts para convertir o migrar datos:
- `convert_lote_id_complete.sql` - Conversión completa de Lote ID
- `convert_seguimiento_lote_id_safe.sql` - Conversión segura de Seguimiento Lote ID
- `convert_seguimiento_lote_id_to_integer.sql` - Conversión a entero
- `migrate_lote_auto_increment.sql` - Migración de auto-increment

### ✅ verify/
Scripts para verificar datos e integridad:
- `verify_linea_genetica_id.sql` - Verificar Linea Genética ID
- `verify_lote_auto_increment.sql` - Verificar auto-increment de Lote
- `verify_seguimiento_lote_data.sql` - Verificar datos de Seguimiento Lote

### 💾 backup/
Backups completos de la base de datos (ubicados en `db/`):
- `db/sanmarino-09-07-2025.sql`
- `db/sanmarino-09-14-2025.sql`
- `db/tabla_menu/menu 25-09-2025.sql`

### 🗄️ Scripts Principales
- `add_traslados_aves_menu.sql` - Agregar menú de traslados de aves
- `create_produccion_tables.sql` - Crear tablas de producción
- `create_produccion_avicola_raw_table.sql` - Tabla de producción avícola raw
- `create_seguimiento_produccion_table.sql` - Tabla de seguimiento de producción
- `script_crear_tabla_produccion_avicola_raw.sql` - Script de creación
- `script_crear_tablas_inventario_aves.sql` - Script de inventario de aves
- `script_crear_tablas_inventario_simple.sql` - Script de inventario simple
- `solucion_final_migraciones.sql` - Solución final de migraciones
- `marcar_migracion_aplicada.sql` - Marcar migración como aplicada

---

## 🎯 Uso

### Ejecutar un Script

**Opción 1: Desde psql**
```bash
psql -U postgres -d sanmarinoapp -f sql/script_name.sql
```

**Opción 2: Desde herramienta GUI**
- pgAdmin
- DBeaver
- TablePlus

### Orden Recomendado para Primera Configuración

1. Crear tablas principales
2. Crear tablas de producción
3. Ejecutar scripts de conversión (si aplica)
4. Verificar con scripts de verify/
5. Agregar menús (add_traslados_aves_menu.sql)

---

## ⚠️ Precauciones

- **Siempre** hacer backup antes de ejecutar scripts de conversión
- **Verificar** el script antes de ejecutarlo en producción
- **Probar** primero en ambiente de desarrollo/staging
- Los scripts de conversión modifican datos existentes

---

## 📝 Notas

- Los backups completos se guardan en la carpeta `db/` del proyecto
- Los scripts de migración son versionados
- Consulte siempre la fecha del script para entender el contexto

---

**Última actualización**: Octubre 2025
