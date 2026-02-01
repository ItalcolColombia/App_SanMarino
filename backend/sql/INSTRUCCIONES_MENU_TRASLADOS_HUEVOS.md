# 📋 Instrucciones para Agregar el Menú de Traslados Huevos

## 🎯 Objetivo
Agregar el menú "Traslados Huevos" a la base de datos para que aparezca en la interfaz del sistema.

## 📊 Información del Menú

- **Label**: "Traslados Huevos"
- **Icon**: "egg"
- **Route**: "/traslados-huevos"
- **Parent ID**: NULL (menú raíz)
- **Order**: 7 (después de "Traslados Aves" que tiene order 6, antes de "Reportes Técnicos" que tiene order 8)
- **Is Active**: true

### Submenús:
- **Nuevo Traslado**: `/traslados-huevos/nuevo` (order: 1)

## 🔧 Opción 1: Ejecutar Script SQL Directamente

### Usando psql:
```bash
psql -U postgres -d sanmarinoapp_local -f backend/sql/add_traslados_huevos_menu.sql
```

### Usando pgAdmin o DBeaver:
1. Abre la herramienta de administración de PostgreSQL
2. Conéctate a la base de datos `sanmarinoapp_local` (o tu base de datos correspondiente)
3. Abre el archivo `backend/sql/add_traslados_huevos_menu.sql`
4. Ejecuta el script

## 🔧 Opción 2: Ejecutar desde el Backend (C#)

Puedes ejecutar el script desde el código C# usando Entity Framework:

```csharp
// En Program.cs o en un endpoint temporal
var sql = File.ReadAllText("sql/add_traslados_huevos_menu.sql");
await context.Database.ExecuteSqlRawAsync(sql);
```

## 🔧 Opción 3: Insertar Manualmente

Ejecuta este SQL directamente:

```sql
-- Insertar menú principal
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Traslados Huevos',
    'egg',
    '/traslados-huevos',
    NULL,
    7,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Traslados Huevos' AND parent_id IS NULL
);

-- Insertar submenú "Nuevo Traslado"
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Nuevo Traslado',
    'truck',
    '/traslados-huevos/nuevo',
    m.id,
    1,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Traslados Huevos' AND m.parent_id IS NULL
AND NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Nuevo Traslado' AND parent_id = m.id
);
```

## ✅ Verificación

Después de ejecutar el script, verifica que el menú se insertó correctamente:

```sql
SELECT 
    m.id,
    m.label,
    m.icon,
    m.route,
    m.parent_id,
    m."order",
    m.is_active,
    pm.label as parent_label
FROM menus m
LEFT JOIN menus pm ON m.parent_id = pm.id
WHERE m.label = 'Traslados Huevos' OR m.parent_id IN (
    SELECT id FROM menus WHERE label = 'Traslados Huevos' AND parent_id IS NULL
)
ORDER BY m.parent_id, m."order";
```

Deberías ver un resultado como:
```
id | label            | icon | route                      | parent_id | order | is_active | parent_label
---|------------------|------|----------------------------|-----------|-------|-----------|-------------
X  | Traslados Huevos | egg  | /traslados-huevos          | NULL      | 7     | true      | NULL
Y  | Nuevo Traslado   | truck| /traslados-huevos/nuevo    | X         | 1     | true      | Traslados Huevos
```

## 📝 Notas

- El script incluye validaciones para evitar duplicados
- El orden (order) puede ajustarse según tus necesidades
- Si necesitas agregar más submenús en el futuro, puedes seguir el mismo patrón
- El icono "egg" es apropiado para representar huevos, pero puedes cambiarlo si prefieres otro icono de FontAwesome

## 🔐 Permisos (Opcional)

Si necesitas asignar permisos específicos a este menú, puedes usar:

```sql
INSERT INTO menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM menus m, permissions p
WHERE m.label = 'Traslados Huevos' 
  AND p.key = 'traslados_huevos'; -- Ajusta la key del permiso según tu sistema
```
