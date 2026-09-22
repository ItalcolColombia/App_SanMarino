# Wiki de GitHub desde `backend/documentacion` (22-sep-2026)

## Pedido

Tener lo de `backend/documentacion` en la wiki de GitHub del repo. Decisión del usuario, **después de
avisarle** que el repo es público (la wiki también lo será y, sin restricción de un admin, la puede
editar cualquier usuario de GitHub) y que la carpeta trae datos de infraestructura y patrones de
credenciales: **publicar la carpeta entera, tal cual**.

## Estado encontrado

- Wiki habilitada (`has_wiki: true`) pero **no inicializada**: GitHub crea `App_SanMarino.wiki.git`
  recién cuando alguien crea la primera página desde la web. No hay API para eso ⇒ paso del usuario.
- `backend/documentacion`: 192 archivos (179 `.md`), 2,1 MB, subcarpetas `aws-infrastructure`,
  `requerimiento-ecuador`, `requerimiento-panama`, `requisito-ciberseguridad`.
- La wiki nombra cada página por su archivo, sin carpeta ⇒ los 3 `README.md` chocan.
- 29 links a otros `.md`, 5 de ellos fuera de la carpeta (`../sql`, `../src`, `fase_de_desarrollo`,
  `tracker_estado.md`) que en la wiki no existen.

## Enfoque

Script reutilizable `backend/scripts/generar-wiki-documentacion.js` que arma el árbol de la wiki (no
publica nada; el push es aparte):

- Contenido **tal cual**; solo se ajusta lo que la wiki necesita para funcionar:
  - `README.md` de subcarpeta → página con el nombre de la carpeta; el de la raíz → `Documentacion`.
  - Links a otro `.md` de la carpeta → nombre de página de wiki (con `#ancla`).
  - Links que salen de la carpeta → URL absoluta al archivo en GitHub (`blob/main/...`).
- Genera `Home.md` (portada con índice por carpeta), `_Sidebar.md` (menú) y `_Footer.md` («generado
  desde `backend/documentacion` @ commit; editar en el repo, no en la wiki»).
- Archivos que no son `.md` se copian igual (quedan como adjuntos).
- Falla si dos páginas terminarían con el mismo nombre.

Sin cambios de backend, frontend, BD ni pipeline. La sincronización automática (workflow en cada push)
queda como paso siguiente opcional.

## Casos de prueba (`node --test`)

- README de subcarpeta → nombre de la carpeta; README raíz → `Documentacion`.
- Link `X.md`, `./X.md`, `sub/X.md#ancla` → `X` / `X#ancla`; link a README de subcarpeta → nombre de
  carpeta.
- Link `../sql/archivo.sql` desde la raíz y desde una subcarpeta → URL absoluta correcta.
- Links externos (`https://`), anclas puras (`#x`) y `mailto:` intactos.
- Colisión de nombres → error.
- Corrida real sobre `backend/documentacion`: 179 páginas + Home/Sidebar/Footer, 0 links a `.md`
  sin resolver.
