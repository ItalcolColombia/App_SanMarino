# Menú: Movimiento de Aves vs Movimiento de Pollo Engorde

## ¿Está "Movimiento de Aves" hardcodeado en el menú?

**No.** El menú del sidebar **no está hardcodeado** en el frontend. Se obtiene siempre del backend:

- **GET `/api/Roles/menus/me`** (o **GET `/api/Auth/menu`** según el flujo).
- El backend construye el menú con `Menus_GetForUserAsync(userId, companyId)` usando **solo** los ítems que están en la tabla **`role_menus`** para los roles del usuario.

Por tanto, si "Movimientos de Aves" sigue apareciendo después de quitarlo del rol, las causas posibles son:

1. **El menú sigue asignado al rol en base de datos**  
   Se quitó en la pantalla de asignación pero no se guardó bien, o se editó otro rol.

2. **El usuario tiene más de un rol**  
   Aunque quites "Movimiento de Aves" de un rol, si el usuario tiene otro rol que sí tiene ese ítem, lo seguirá viendo (el menú es la unión de todos los menús de sus roles).

3. **Caché del menú en el navegador**  
   El menú se guarda en sesión (localStorage/sessionStorage). Hasta que no se vuelva a pedir el menú al backend, puede seguir mostrándose la versión anterior.

## Qué hacer paso a paso

### 1. Verificar en base de datos

Ejecuta este SQL para ver qué roles tienen asignado "Movimientos de Aves" y "Movimiento de Pollo Engorde":

```sql
-- Íds de los ítems de menú
SELECT id, label, route FROM menus
WHERE route IN ('/movimientos-aves/lista', '/movimiento-pollo-engorde/lista')
   OR label IN ('Movimientos de Aves', 'Movimiento de Pollo Engorde');

-- Roles que tienen "Movimientos de Aves"
SELECT r.id AS role_id, r.name AS role_name, m.id AS menu_id, m.label, m.route
FROM role_menus rm
JOIN roles r ON r.id = rm.role_id
JOIN menus m ON m.id = rm.menu_id
WHERE m.route = '/movimientos-aves/lista' OR m.label = 'Movimientos de Aves';

-- Roles que tienen "Movimiento de Pollo Engorde"
SELECT r.id AS role_id, r.name AS role_name, m.id AS menu_id, m.label, m.route
FROM role_menus rm
JOIN roles r ON r.id = rm.role_id
JOIN menus m ON m.id = rm.menu_id
WHERE m.route = '/movimiento-pollo-engorde/lista' OR m.label = 'Movimiento de Pollo Engorde';
```

- Si el rol del usuario **sigue apareciendo** en la primera consulta (Movimientos de Aves), hay que **quitar** esa fila de `role_menus` para ese rol (o quitar el ítem desde la pantalla de asignación de menús al rol y guardar).
- Si el usuario tiene **varios roles**, revisa todos: mientras alguno tenga "Movimientos de Aves", lo verá en el menú.

### 2. Quitar "Movimientos de Aves" del rol (solo si hace falta por BD)

Solo si en tu flujo no usas la pantalla de roles para quitar el ítem:

```sql
-- Sustituir NNN por el id del rol y MMM por el id del menú "Movimientos de Aves"
DELETE FROM role_menus
WHERE role_id = NNN AND menu_id = (SELECT id FROM menus WHERE route = '/movimientos-aves/lista' LIMIT 1);
```

### 3. Refrescar el menú en el navegador

- **Cerrar sesión y volver a iniciar sesión**  
  Así se vuelve a llamar a la API del menú y se actualiza la sesión con los ítems actuales (sin "Movimiento de Aves" si ya no está en `role_menus`).

- **Desde la pantalla de asignación de menús al rol**  
  Si un admin guarda cambios de menú de un rol, la aplicación ahora **refresca el menú del sidebar** automáticamente. Si el usuario afectado es el que está logueado, debería ver el menú actualizado sin tener que cerrar sesión (o puede ser necesario recargar la página si el sidebar no se actualiza solo).

## Resumen

| Pregunta | Respuesta |
|----------|-----------|
| ¿"Movimiento de Aves" está hardcodeado en el frontend? | **No.** |
| ¿De dónde sale el menú? | Backend: `GET /api/Roles/menus/me` (o `/api/Auth/menu`), según `role_menus` y roles del usuario. |
| Si lo quito del rol y sigue apareciendo | Revisar: 1) que el rol guardado ya no tenga ese ítem en `role_menus`, 2) que el usuario no tenga otro rol que sí lo tenga, 3) cerrar sesión y volver a entrar (o refrescar después de guardar el rol). |

Para que un usuario **solo** vea "Movimiento de Pollo Engorde" y no "Movimientos de Aves":

1. Quitar "Movimientos de Aves" de **todos** los roles de ese usuario (en la UI de roles o en BD).
2. Asegurar que al menos uno de sus roles tenga "Movimiento de Pollo Engorde".
3. Cerrar sesión e iniciar sesión de nuevo (o usar el refresco del menú al guardar el rol).
