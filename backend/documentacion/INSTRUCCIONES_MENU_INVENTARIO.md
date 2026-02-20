# 📋 Instrucciones para Agregar el Menú de Gestión de Inventario

## 🎯 Objetivo
Agregar el menú "Gestión de Inventario" a la base de datos para que aparezca en la interfaz del sistema.

## 📊 Información del Menú

### Menú Principal:
- **Label**: "Gestión de Inventario"
- **Icon**: "warehouse"
- **Route**: "/inventario"
- **Parent ID**: NULL (menú raíz)
- **Order**: Se asigna automáticamente (después del último menú)
- **Key**: "inventory_management" (único, obligatorio)
- **Sort Order**: 0
- **Is Group**: false (es un menú simple, no tiene submenús)
- **Is Active**: true

### Pestañas Internas (NO son submenús):
Las siguientes opciones se manejan como **pestañas internas** en el componente `InventarioTabsComponent`:
1. **Movimientos** - Pestaña dentro del componente
2. **Movimiento de Alimento** - Pestaña dentro del componente
3. **Stock** - Pestaña dentro del componente
4. **Kardex** - Pestaña dentro del componente
5. **Catálogo de Productos** - Pestaña dentro del componente

**Nota:** NO se crean submenús en la base de datos porque son redundantes. El componente `InventarioTabsComponent` maneja todas las pestañas internamente. Al hacer clic en "Gestión de Inventario", se carga el componente con todas las pestañas disponibles en la parte superior.

## 🔧 Opción 1: Ejecutar Script SQL Directamente

### Usando psql:
```bash
psql -U postgres -d nombre_base_datos -f backend/sql/add_inventario_menu.sql
```

### Usando pgAdmin o DBeaver:
1. Abre la herramienta de administración de PostgreSQL
2. Conéctate a la base de datos correspondiente
3. Abre el archivo `backend/sql/add_inventario_menu.sql`
4. Ejecuta el script

## 🔧 Opción 2: Ejecutar desde el Backend (C#)

Puedes ejecutar el script desde el código C# usando Entity Framework:

```csharp
// En Program.cs o en un endpoint temporal
var sql = File.ReadAllText("sql/add_inventario_menu.sql");
await context.Database.ExecuteSqlRawAsync(sql);
```

## 🔧 Opción 3: Insertar Manualmente

Ejecuta este SQL directamente:

```sql
-- Insertar SOLO el menú principal (sin submenús)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
SELECT 
    'Gestión de Inventario',
    'warehouse',
    '/inventario',
    NULL,
    (SELECT COALESCE(MAX("order"), 0) + 1 FROM menus WHERE parent_id IS NULL),
    true,
    'inventory_management',
    0,
    false  -- NO es un grupo, es un menú simple
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE key = 'inventory_management' AND parent_id IS NULL
);
```

## ✅ Verificación

Después de ejecutar el script, verifica que el menú se agregó correctamente:

```sql
-- Verificar que se insertó correctamente
SELECT 
    m.id,
    m.label,
    m.icon,
    m.route,
    m.parent_id,
    m."order",
    m.is_active,
    m.key,
    m.sort_order,
    m.is_group
FROM menus m
WHERE m.key = 'inventory_management' AND m.parent_id IS NULL;
```

## 🎨 Iconos Disponibles

Los iconos usan FontAwesome. Algunos iconos comunes:
- `warehouse` - Almacén/Bodega
- `arrows-up-down` - Movimientos
- `wheat-awn` - Alimento/Grano
- `list` - Lista/Kardex
- `book` - Catálogo/Libro
- `boxes-alt` - Cajas/Productos
- `tachometer-alt` - Dashboard

## 📝 Notas

1. **Rutas:** Todos los submenús apuntan a `/inventario` porque el componente `InventarioTabsComponent` maneja las pestañas internamente. El usuario puede cambiar entre pestañas sin cambiar la ruta.

2. **Permisos:** Si tu sistema usa permisos, necesitarás crear los permisos correspondientes y vincularlos a los menús usando la tabla `menu_permissions`.

3. **Orden:** El script asigna automáticamente el orden después del último menú existente. Si quieres un orden específico, puedes modificar el script.

## 🔄 Actualizar el Menú en la Aplicación

Después de ejecutar el SQL:
1. Cierra sesión y vuelve a iniciar sesión (para recargar el menú desde la base de datos)
2. O espera a que el sistema recargue automáticamente el menú (depende de la configuración)

---

**Última actualización:** 2024-02-03
