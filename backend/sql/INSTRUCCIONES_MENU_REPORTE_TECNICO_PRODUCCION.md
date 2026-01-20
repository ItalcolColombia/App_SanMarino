# 📋 Instrucciones para Agregar el Menú de Reporte Técnico Producción SanMarino

## 🎯 Objetivo
Agregar el menú "Reporte Técnico Producción SanMarino" a la base de datos para que aparezca en la interfaz del sistema.

## 📊 Información del Menú

- **Label**: "Reporte Técnico Producción SanMarino"
- **Icon**: "chart-line"
- **Route**: "/reporte-tecnico-produccion"
- **Parent ID**: NULL (menú raíz)
- **Order**: 11 (ajusta según la posición deseada en el menú)
- **Is Active**: true

## 🔧 Opción 1: Ejecutar Script SQL Directamente

### Usando psql:
```bash
psql -U postgres -d sanmarinoapp_local -f backend/sql/add_reporte_tecnico_produccion_menu.sql
```

### Usando pgAdmin o DBeaver:
1. Abre la herramienta de administración de PostgreSQL
2. Conéctate a la base de datos `sanmarinoapp_local` (o tu base de datos correspondiente)
3. Abre el archivo `backend/sql/add_reporte_tecnico_produccion_menu.sql`
4. Ejecuta el script

## 🔧 Opción 2: Ejecutar desde el Backend (C#)

Puedes ejecutar el script desde el código C# usando Entity Framework:

```csharp
// En Program.cs o en un endpoint temporal
var sql = File.ReadAllText("sql/add_reporte_tecnico_produccion_menu.sql");
await context.Database.ExecuteSqlRawAsync(sql);
```

## 🔧 Opción 3: Insertar Manualmente

Ejecuta este SQL directamente:

```sql
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Reporte Técnico Producción SanMarino',
    'chart-line',
    '/reporte-tecnico-produccion',
    NULL,
    11,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Reporte Técnico Producción SanMarino' AND parent_id IS NULL
);
```

## ✅ Verificación

Después de ejecutar el script, verifica que el menú se insertó correctamente:

```sql
SELECT 
    id,
    label,
    icon,
    route,
    parent_id,
    "order",
    is_active
FROM menus
WHERE label = 'Reporte Técnico Producción SanMarino';
```

Deberías ver un resultado como:
```
id | label                                    | icon       | route                        | parent_id | order | is_active
---|------------------------------------------|------------|------------------------------|-----------|-------|----------
XX | Reporte Técnico Producción SanMarino    | chart-line | /reporte-tecnico-produccion  | NULL      | 11    | true
```

## 📝 Ajustar el Orden del Menú

Si necesitas cambiar el orden del menú, primero verifica qué órdenes están en uso:

```sql
SELECT 
    id,
    label,
    "order"
FROM menus
WHERE parent_id IS NULL
ORDER BY "order";
```

Luego, actualiza el orden si es necesario:

```sql
UPDATE menus
SET "order" = 11  -- Cambia este número según donde quieras que aparezca
WHERE label = 'Reporte Técnico Producción SanMarino' AND parent_id IS NULL;
```

## 🔐 Asignar Permisos (Opcional)

Si necesitas que el menú requiera permisos específicos, puedes asignarlos:

```sql
-- Primero, verifica qué permisos existen
SELECT id, key, name FROM permissions WHERE key LIKE '%reporte%' OR key LIKE '%produccion%';

-- Luego, asigna el permiso al menú (ajusta el permission_id según tu sistema)
INSERT INTO menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM menus m, permissions p
WHERE m.label = 'Reporte Técnico Producción SanMarino' 
  AND p.key = 'reporte_tecnico_produccion'; -- Ajusta según tu sistema de permisos
```

## 📝 Notas

- El script es **idempotente**: puedes ejecutarlo múltiples veces sin crear duplicados
- El menú aparecerá en la interfaz después de recargar la página
- Si no aparece, verifica que:
  - El usuario tenga los permisos necesarios
  - El menú esté asignado a su rol
  - El menú esté activo (`is_active = true`)

## 🚀 Próximos Pasos

1. Ejecutar el script SQL
2. Verificar que el menú se insertó correctamente
3. Recargar la aplicación frontend
4. El menú debería aparecer en la barra lateral
5. Navegar a `/reporte-tecnico-produccion` para acceder al módulo

## 🔗 Ruta del Módulo

La ruta configurada en el frontend es:
- **Ruta Angular**: `/reporte-tecnico-produccion`
- **Módulo**: `ReporteTecnicoProduccionModule`
- **Componente Principal**: `ReporteTecnicoProduccionMainComponent`
