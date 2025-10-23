# 📁 Scripts SQL - ZooSanMarino

Esta carpeta contiene todos los scripts SQL para la base de datos PostgreSQL del proyecto ZooSanMarino.

## 📋 **Archivos Disponibles**

### 🐣 **Sistema de Producción Avícola**
- **`create_produccion_avicola_raw_table.sql`** - Crear tabla produccion_avicola_raw (versión inicial)
- **`script_crear_tabla_produccion_avicola_raw.sql`** - Script completo para tabla produccion_avicola_raw
- **`marcar_migracion_aplicada.sql`** - Marcar migración como aplicada en EF Core
- **`solucion_final_migraciones.sql`** - Solución para problemas de sincronización de migraciones

### 🐦 **Sistema de Inventario de Aves** 
- **`script_crear_tablas_inventario_aves.sql`** - **[RECOMENDADO]** Script completo con todas las características
- **`script_crear_tablas_inventario_simple.sql`** - Script simplificado sin claves foráneas

## 🚀 **Instrucciones de Uso**

### **Para Sistema de Inventario de Aves (NUEVO)**

#### Opción 1: Script Completo (Recomendado)
```bash
psql -h tu_host -U tu_usuario -d tu_base_datos -f sql/script_crear_tablas_inventario_aves.sql
```

#### Opción 2: Script Simple (Si hay problemas)
```bash
psql -h tu_host -U tu_usuario -d tu_base_datos -f sql/script_crear_tablas_inventario_simple.sql
```

### **Para Sistema de Producción Avícola**

#### Crear tabla produccion_avicola_raw
```bash
psql -h tu_host -U tu_usuario -d tu_base_datos -f sql/script_crear_tabla_produccion_avicola_raw.sql
```

#### Marcar migración como aplicada
```bash
psql -h tu_host -U tu_usuario -d tu_base_datos -f sql/solucion_final_migraciones.sql
```

## 📊 **Tablas que se Crean**

### **Sistema de Inventario de Aves**
1. **`inventario_aves`** - Inventario actual por ubicación
2. **`movimiento_aves`** - Traslados y movimientos
3. **`historial_inventario`** - Historial de cambios

### **Sistema de Producción Avícola**
1. **`produccion_avicola_raw`** - Datos de producción avícola

## ⚠️ **Notas Importantes**

- **Ejecutar en orden:** Primero producción avícola, luego inventario de aves
- **Backup recomendado:** Hacer respaldo antes de ejecutar scripts
- **Scripts idempotentes:** Se pueden ejecutar múltiples veces sin problemas
- **Compatibilidad:** Diseñados para PostgreSQL 12+

## 🔧 **Solución de Problemas**

### Si hay errores de claves foráneas:
1. Usar el script simple: `script_crear_tablas_inventario_simple.sql`
2. Agregar claves foráneas manualmente después

### Si hay problemas de migraciones:
1. Ejecutar: `solucion_final_migraciones.sql`
2. Verificar tabla `__EFMigrationsHistory`

## 📞 **Soporte**

Para problemas con los scripts SQL, verificar:
1. Conexión a PostgreSQL
2. Permisos de usuario
3. Existencia de tablas referenciadas
4. Versión de PostgreSQL compatible

---
**Última actualización:** Octubre 2025  
**Versión del proyecto:** 1.0.0
