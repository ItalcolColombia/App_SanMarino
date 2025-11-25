# 📋 Instrucciones para Agregar el Menú de Reportes Técnicos

## 🎯 Objetivo
Agregar el menú "Reportes Técnicos" a la base de datos para que aparezca en la interfaz del sistema.

## 📊 Información del Menú

- **Label**: "Reportes Técnicos"
- **Icon**: "file-alt"
- **Route**: "/reportes-tecnicos"
- **Parent ID**: NULL (menú raíz)
- **Order**: 8 (después de "Traslados Aves" que tiene order 7)
- **Is Active**: true

## 🔧 Opción 1: Ejecutar Script SQL Directamente

### Usando psql:
```bash
psql -U postgres -d sanmarinoapp_local -f backend/sql/add_reportes_tecnicos_menu_simple.sql
```

### Usando pgAdmin o DBeaver:
1. Abre la herramienta de administración de PostgreSQL
2. Conéctate a la base de datos `sanmarinoapp_local`
3. Abre el archivo `backend/sql/add_reportes_tecnicos_menu_simple.sql`
4. Ejecuta el script

## 🔧 Opción 2: Ejecutar desde el Backend (C#)

Puedes ejecutar el script desde el código C# usando Entity Framework:

```csharp
// En Program.cs o en un endpoint temporal
var sql = File.ReadAllText("sql/add_reportes_tecnicos_menu_simple.sql");
await context.Database.ExecuteSqlRawAsync(sql);
```

## 🔧 Opción 3: Insertar Manualmente

Ejecuta este SQL directamente:

```sql
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Reportes Técnicos',
    'file-alt',
    '/reportes-tecnicos',
    NULL,
    8,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Reportes Técnicos' AND parent_id IS NULL
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
WHERE label = 'Reportes Técnicos';
```

Deberías ver un resultado como:
```
id | label              | icon     | route                | parent_id | order | is_active
---|--------------------|----------|----------------------|-----------|-------|----------
19 | Reportes Técnicos   | file-alt | /reportes-tecnicos  | NULL      | 8     | true
```

## 🔐 Asignar Permisos (Opcional)

Si necesitas que el menú requiera permisos específicos, puedes asignarlos:

```sql
-- Primero, verifica qué permisos existen
SELECT id, key, name FROM permissions WHERE key LIKE '%reporte%';

-- Luego, asigna el permiso al menú (ajusta el permission_id según tu sistema)
INSERT INTO menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM menus m, permissions p
WHERE m.label = 'Reportes Técnicos' 
  AND p.key = 'reportes_tecnicos'; -- Ajusta según tu sistema de permisos
```

## 📝 Notas

- El script es **idempotente**: puedes ejecutarlo múltiples veces sin crear duplicados
- El menú aparecerá en la interfaz después de recargar la página
- Si no aparece, verifica que el usuario tenga los permisos necesarios o que el menú esté asignado a su rol

## 🚀 Próximos Pasos

1. Ejecutar el script SQL
2. Verificar que el menú se insertó correctamente
3. Recargar la aplicación frontend
4. El menú debería aparecer en la barra lateral


