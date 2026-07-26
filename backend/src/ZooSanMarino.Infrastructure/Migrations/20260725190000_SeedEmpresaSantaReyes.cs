using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de <b>solo datos</b> (sin cambios de schema) que crea el arranque completo de la empresa
    /// <b>Santa Reyes</b> (Colombia, postura comercial) sobre el schema de
    /// <c>20260725175311_AddInfraErpAvicolaSantaReyes</c>.
    ///
    /// Bloques, en orden: empresa + pais + flag ERP → lista maestra regional → roles + permisos →
    /// menus (clonados de Agroavicola Sanmarino, sin engorde/Panama/config global) → usuarios
    /// (logins/users/user_logins/user_companies/user_roles) → granja → nucleos → galpones → silos →
    /// user_farms → catalogo de items → items de alimento al inventario → lotes + espejos
    /// (lote_etapa_levante, lote_postura_levante y, para los que ya superan la semana 26,
    /// lote_postura_produccion).
    ///
    /// <b>Cero ids de BD hardcodeados</b>: empresa por <c>name</c>, pais por <c>pais_nombre</c>,
    /// menus por <c>route</c>/<c>label</c>, departamento/municipio por nombre, lista maestra por
    /// <c>key</c>+empresa+pais, permisos por tabla completa, <c>galpon_id</c> derivado del maximo
    /// vigente. Los ids difieren entre local y produccion.
    ///
    /// <b>Idempotente</b>: todo INSERT va con <c>WHERE NOT EXISTS</c> y los UPDATE llevan guarda
    /// <c>IS DISTINCT FROM</c>, de modo que una segunda pasada no cambia ni una fila (importante:
    /// <c>lote_postura_levante</c>/<c>_produccion</c> tienen triggers de historico AFTER
    /// INSERT/UPDATE que dejarian rastro si el UPDATE se ejecutara en vacio).
    ///
    /// <b>Tolerante a triggers</b>: en la BD local existe <c>trg_lotes_sync_lote_postura_levante</c>
    /// (crea el espejo de levante al insertar en <c>lotes</c>), pero produccion puede diferir. Por eso
    /// el espejo se resuelve como UPSERT: INSERT solo si falta + UPDATE a los valores deseados.
    ///
    /// La <c>fase</c> del lote replica exactamente <c>LoteService.CreateAsync</c>
    /// (<c>CalcularSemanasDesdeEncaset</c> = <c>(dias / 7) + 1</c>; &gt;= 26 → <c>Produccion</c>),
    /// evaluada contra la fecha de ejecucion, y las fechas de encaset se anclan a las <b>12:00 UTC</b>
    /// (regla de fechas puras del repo) para que no se corran un dia.
    /// </summary>
    public partial class SeedEmpresaSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $sr$
DECLARE
    v_pais_id         integer;
    v_company_id      integer;
    v_master_list_id  integer;
    v_regional_id     integer;
    v_rol_admin_id    integer;
    v_rol_impl_id     integer;
    v_login_admin_id  uuid;
    v_login_impl_id   uuid;
    v_user_admin_id   uuid;
    v_user_impl_id    uuid;
    v_departamento_id integer;
    v_municipio_id    integer;
    v_farm_id         integer;

    -- Hash PBKDF2 V3 (ASP.NET Identity PasswordHasher) de la contrasena '123456789'.
    -- No es reproducible en SQL puro: se genera offline y se incrusta como literal.
    c_hash        constant text := 'AQAAAAIAAYagAAAAELfUHTeZsq8qy4PZIliKn6nfeYJyq77UR8tG6bOjmbr2c95nGhysxHokYIUW3O2s6w==';
    c_empresa     constant text := 'Santa Reyes';
    c_empresa_src constant text := 'Agroavicola Sanmarino';   -- empresa origen de los menus
    c_granja      constant text := 'La Esperanza';
    c_rol_admin   constant text := 'Santa Reyes Administrador';
    c_rol_impl    constant text := 'Santa Reyes Implementador';
    c_email_admin constant text := 'admin@santareyes.com';
    c_email_impl  constant text := 'implementador@santareyes.com';
BEGIN
    -- =================================================================================
    -- 0) PAIS — Colombia por nombre (los pais_id pueden diferir entre entornos)
    -- =================================================================================
    SELECT p.pais_id INTO v_pais_id
    FROM public.paises p
    WHERE lower(p.pais_nombre) = 'colombia'
    ORDER BY p.pais_id
    LIMIT 1;

    IF v_pais_id IS NULL THEN
        RAISE EXCEPTION 'SeedEmpresaSantaReyes: no existe el pais Colombia en public.paises.';
    END IF;

    -- =================================================================================
    -- 1) EMPRESA — Santa Reyes (Colombia) + pais + flag de codigos ERP avicolas
    --    maneja_codigos_erp_avicola = true es lo que hace visibles los campos ERP en el
    --    front (flag tipado por comportamiento; jamas ""if empresa == X"" en codigo).
    -- =================================================================================
    INSERT INTO public.companies
           (name, identifier, document_type, address, phone, email,
            country, state, city, visual_permissions,
            mobile_access, maneja_alimento_por_galpon, maneja_codigos_erp_avicola)
    SELECT c_empresa, '901000001-1', 'NIT', 'Km 5 vía Buga', '6021000000', 'contacto@santareyes.com',
           'Colombia', 'Valle del Cauca', 'Buga',
           ARRAY['dashboard', 'reports', 'farms', 'users']::text[],
           false, false, true
    WHERE NOT EXISTS (SELECT 1 FROM public.companies c WHERE c.name = c_empresa);

    SELECT c.id INTO v_company_id
    FROM public.companies c
    WHERE c.name = c_empresa
    ORDER BY c.id
    LIMIT 1;

    UPDATE public.companies
       SET maneja_codigos_erp_avicola = true
     WHERE id = v_company_id
       AND maneja_codigos_erp_avicola IS DISTINCT FROM true;

    INSERT INTO public.company_pais (company_id, pais_id, created_at)
    SELECT v_company_id, v_pais_id, now()
    WHERE NOT EXISTS (SELECT 1 FROM public.company_pais cp
                       WHERE cp.company_id = v_company_id AND cp.pais_id = v_pais_id);

    -- =================================================================================
    -- 2) LISTA MAESTRA REGIONAL — region_option_key scopeada (empresa + pais) con la
    --    opcion 'Occidente'. Las regionales de granja viven en master_lists, no en la
    --    tabla `regionales` (farms.regional_id guarda el id de la OPCION).
    -- =================================================================================
    INSERT INTO public.master_lists (key, name, company_id, company_name, country_id, country_name)
    SELECT 'region_option_key',
           COALESCE((SELECT ml.name
                       FROM public.master_lists ml
                       JOIN public.companies co ON co.id = ml.company_id
                      WHERE ml.key = 'region_option_key'
                        AND ml.country_id = v_pais_id
                        AND co.name = c_empresa_src
                      ORDER BY ml.id
                      LIMIT 1), 'Regionales'),
           v_company_id, c_empresa, v_pais_id, 'Colombia'
    WHERE NOT EXISTS (SELECT 1 FROM public.master_lists ml
                       WHERE ml.key = 'region_option_key'
                         AND ml.company_id = v_company_id
                         AND ml.country_id = v_pais_id);

    SELECT ml.id INTO v_master_list_id
    FROM public.master_lists ml
    WHERE ml.key = 'region_option_key'
      AND ml.company_id = v_company_id
      AND ml.country_id = v_pais_id
    ORDER BY ml.id
    LIMIT 1;

    INSERT INTO public.master_list_options (master_list_id, value, ""order"")
    SELECT v_master_list_id, 'Occidente', 1
    WHERE NOT EXISTS (SELECT 1 FROM public.master_list_options o
                       WHERE o.master_list_id = v_master_list_id AND o.value = 'Occidente');

    SELECT o.id INTO v_regional_id
    FROM public.master_list_options o
    WHERE o.master_list_id = v_master_list_id AND o.value = 'Occidente'
    ORDER BY o.id
    LIMIT 1;

    -- =================================================================================
    -- 3) ROLES — Administrador e Implementador de Santa Reyes (mismos permisos).
    --    is_company_admin = true: administran su empresa (no son super admin).
    -- =================================================================================
    INSERT INTO public.roles (name, description, allow_multiple_countries, allow_multiple_companies, is_company_admin)
    SELECT c_rol_admin,
           'Administrador de la empresa Santa Reyes: acceso total a los modulos, granjas y usuarios de la empresa.',
           false, false, true
    WHERE NOT EXISTS (SELECT 1 FROM public.roles r WHERE r.name = c_rol_admin);

    INSERT INTO public.roles (name, description, allow_multiple_countries, allow_multiple_companies, is_company_admin)
    SELECT c_rol_impl,
           'Implementador de la empresa Santa Reyes: mismos permisos que el administrador, para la puesta en marcha.',
           false, false, true
    WHERE NOT EXISTS (SELECT 1 FROM public.roles r WHERE r.name = c_rol_impl);

    SELECT r.id INTO v_rol_admin_id FROM public.roles r WHERE r.name = c_rol_admin;
    SELECT r.id INTO v_rol_impl_id  FROM public.roles r WHERE r.name = c_rol_impl;

    INSERT INTO public.role_companies (role_id, company_id)
    SELECT r.id, v_company_id
    FROM (VALUES (v_rol_admin_id), (v_rol_impl_id)) AS r(id)
    WHERE NOT EXISTS (SELECT 1 FROM public.role_companies rc
                       WHERE rc.role_id = r.id AND rc.company_id = v_company_id);

    -- TODOS los permisos existentes (por tabla completa, sin ids fijos)
    INSERT INTO public.role_permissions (role_id, permission_id)
    SELECT r.id, p.id
    FROM (VALUES (v_rol_admin_id), (v_rol_impl_id)) AS r(id)
    CROSS JOIN public.permissions p
    WHERE NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                       WHERE rp.role_id = r.id AND rp.permission_id = p.id);

    -- =================================================================================
    -- 4) MENUS — se clonan los de Agroavicola Sanmarino (Colombia) respetando
    --    is_enabled/sort_order/parent_menu_id, EXCLUYENDO lo que no aplica a Santa Reyes:
    --    Panama, engorde y la configuracion global (empresas / geografia).
    --    Se conserva /config/item-inventario-ecuador: es el catalogo de items neutro.
    --    role_menus = exactamente el mismo set, para los 2 roles nuevos.
    -- =================================================================================
    INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
    SELECT v_company_id, cm.menu_id, cm.is_enabled, cm.sort_order, cm.parent_menu_id
    FROM public.company_menus cm
    JOIN public.menus m      ON m.id = cm.menu_id
    JOIN public.companies co ON co.id = cm.company_id
    WHERE co.name = c_empresa_src
      AND NOT (COALESCE(m.label, '') ILIKE '%panam%'
            OR COALESCE(m.route, '') ILIKE '%panama%'
            OR COALESCE(m.route, '') = '/config/companies'
            OR COALESCE(m.route, '') = '/config/countries'
            OR COALESCE(m.route, '') ILIKE '%engorde%'
            OR COALESCE(m.label, '') ILIKE '%engorde%')
      AND NOT EXISTS (SELECT 1 FROM public.company_menus x
                       WHERE x.company_id = v_company_id AND x.menu_id = cm.menu_id);

    INSERT INTO public.role_menus (role_id, menu_id)
    SELECT r.id, cm.menu_id
    FROM public.company_menus cm
    CROSS JOIN (VALUES (v_rol_admin_id), (v_rol_impl_id)) AS r(id)
    WHERE cm.company_id = v_company_id
      AND NOT EXISTS (SELECT 1 FROM public.role_menus rm
                       WHERE rm.role_id = r.id AND rm.menu_id = cm.menu_id);

    -- =================================================================================
    -- 5) USUARIOS — admin@ e implementador@ (contrasena 123456789).
    --    Idempotencia por email en `logins`; el user se localiza por su login y, como
    --    respaldo, por cedula.
    -- =================================================================================
    -- 5a) Administrador -----------------------------------------------------------------
    INSERT INTO public.logins (id, email, password_hash, is_email_login, is_deleted)
    SELECT gen_random_uuid(), c_email_admin, c_hash, true, false
    WHERE NOT EXISTS (SELECT 1 FROM public.logins l WHERE lower(l.email) = c_email_admin);

    SELECT l.id INTO v_login_admin_id
    FROM public.logins l WHERE lower(l.email) = c_email_admin ORDER BY l.id LIMIT 1;

    SELECT ul.user_id INTO v_user_admin_id
    FROM public.user_logins ul WHERE ul.login_id = v_login_admin_id LIMIT 1;

    IF v_user_admin_id IS NULL THEN
        SELECT u.id INTO v_user_admin_id FROM public.users u WHERE u.cedula = '1000000001' ORDER BY u.id LIMIT 1;
    END IF;

    IF v_user_admin_id IS NULL THEN
        INSERT INTO public.users (id, sur_name, first_name, cedula, telefono, ubicacion,
                                  is_active, is_locked, failed_attempts, created_at, updated_at)
        VALUES (gen_random_uuid(), 'Santa Reyes', 'Admin', '1000000001', '3000000001',
                'Buga, Valle del Cauca', true, false, 0, now(), now())
        RETURNING id INTO v_user_admin_id;
    END IF;

    INSERT INTO public.user_logins (user_id, login_id, is_locked_by_admin)
    SELECT v_user_admin_id, v_login_admin_id, false
    WHERE NOT EXISTS (SELECT 1 FROM public.user_logins ul
                       WHERE ul.user_id = v_user_admin_id AND ul.login_id = v_login_admin_id);

    INSERT INTO public.user_companies (user_id, company_id, is_default, pais_id)
    SELECT v_user_admin_id, v_company_id, true, v_pais_id
    WHERE NOT EXISTS (SELECT 1 FROM public.user_companies uc
                       WHERE uc.user_id = v_user_admin_id
                         AND uc.company_id = v_company_id
                         AND uc.pais_id = v_pais_id);

    INSERT INTO public.user_roles (user_id, role_id, company_id)
    SELECT v_user_admin_id, v_rol_admin_id, v_company_id
    WHERE NOT EXISTS (SELECT 1 FROM public.user_roles ur
                       WHERE ur.user_id = v_user_admin_id AND ur.role_id = v_rol_admin_id);

    -- 5b) Implementador -----------------------------------------------------------------
    INSERT INTO public.logins (id, email, password_hash, is_email_login, is_deleted)
    SELECT gen_random_uuid(), c_email_impl, c_hash, true, false
    WHERE NOT EXISTS (SELECT 1 FROM public.logins l WHERE lower(l.email) = c_email_impl);

    SELECT l.id INTO v_login_impl_id
    FROM public.logins l WHERE lower(l.email) = c_email_impl ORDER BY l.id LIMIT 1;

    SELECT ul.user_id INTO v_user_impl_id
    FROM public.user_logins ul WHERE ul.login_id = v_login_impl_id LIMIT 1;

    IF v_user_impl_id IS NULL THEN
        SELECT u.id INTO v_user_impl_id FROM public.users u WHERE u.cedula = '1000000002' ORDER BY u.id LIMIT 1;
    END IF;

    IF v_user_impl_id IS NULL THEN
        INSERT INTO public.users (id, sur_name, first_name, cedula, telefono, ubicacion,
                                  is_active, is_locked, failed_attempts, created_at, updated_at)
        VALUES (gen_random_uuid(), 'Santa Reyes', 'Implementador', '1000000002', '3000000002',
                'Buga, Valle del Cauca', true, false, 0, now(), now())
        RETURNING id INTO v_user_impl_id;
    END IF;

    INSERT INTO public.user_logins (user_id, login_id, is_locked_by_admin)
    SELECT v_user_impl_id, v_login_impl_id, false
    WHERE NOT EXISTS (SELECT 1 FROM public.user_logins ul
                       WHERE ul.user_id = v_user_impl_id AND ul.login_id = v_login_impl_id);

    INSERT INTO public.user_companies (user_id, company_id, is_default, pais_id)
    SELECT v_user_impl_id, v_company_id, true, v_pais_id
    WHERE NOT EXISTS (SELECT 1 FROM public.user_companies uc
                       WHERE uc.user_id = v_user_impl_id
                         AND uc.company_id = v_company_id
                         AND uc.pais_id = v_pais_id);

    INSERT INTO public.user_roles (user_id, role_id, company_id)
    SELECT v_user_impl_id, v_rol_impl_id, v_company_id
    WHERE NOT EXISTS (SELECT 1 FROM public.user_roles ur
                       WHERE ur.user_id = v_user_impl_id AND ur.role_id = v_rol_impl_id);

    -- =================================================================================
    -- 6) GRANJA 'La Esperanza' (Buga, Valle del Cauca) + codigos ERP.
    --    Municipio por nombre EXACTO ('Buga', o 'Guadalajara de Buga' donde asi figure)
    --    y con join al departamento: existe 'Bugaba' (Panama) que no debe matchear.
    -- =================================================================================
    SELECT d.departamento_id INTO v_departamento_id
    FROM public.departamentos d
    WHERE d.pais_id = v_pais_id
      AND d.departamento_nombre ILIKE 'valle del cauca'
    ORDER BY d.departamento_id
    LIMIT 1;

    IF v_departamento_id IS NULL THEN
        RAISE EXCEPTION 'SeedEmpresaSantaReyes: no existe el departamento Valle del Cauca (pais %).', v_pais_id;
    END IF;

    SELECT m.municipio_id INTO v_municipio_id
    FROM public.municipios m
    WHERE m.departamento_id = v_departamento_id AND m.nombre = 'Buga'
    ORDER BY m.municipio_id
    LIMIT 1;

    IF v_municipio_id IS NULL THEN
        SELECT m.municipio_id INTO v_municipio_id
        FROM public.municipios m
        WHERE m.departamento_id = v_departamento_id AND m.nombre = 'Guadalajara de Buga'
        ORDER BY m.municipio_id
        LIMIT 1;
    END IF;

    IF v_municipio_id IS NULL THEN
        RAISE EXCEPTION 'SeedEmpresaSantaReyes: no existe el municipio Buga en el departamento %.', v_departamento_id;
    END IF;

    INSERT INTO public.farms
           (name, regional_id, status, municipio_id, departamento_id,
            company_id, created_by_user_id, created_at, certificado_gab,
            codigo_bodega, descripcion_bodega,
            centro_operacion, descripcion_centro_operacion,
            codigo_instalacion, descripcion_instalacion)
    SELECT c_granja, v_regional_id, 'A', v_municipio_id, v_departamento_id,
           v_company_id, 1, now(), false,
           'B0601', 'BUG GR ESPERANZA ALIMENTO - INSUMOS',
           '830', 'GRANJA LA ESPERANZA BUGA',
           'B06', 'BUG GRANJA LA ESPERANZA'
    WHERE NOT EXISTS (SELECT 1 FROM public.farms f
                       WHERE f.company_id = v_company_id AND f.name = c_granja);

    SELECT f.id INTO v_farm_id
    FROM public.farms f
    WHERE f.company_id = v_company_id AND f.name = c_granja
    ORDER BY f.id
    LIMIT 1;

    -- =================================================================================
    -- 7) NUCLEOS — 3, con nucleo_id fijo (la PK es (nucleo_id, granja_id), no global).
    --    Las descripciones de bodega van VERBATIM del Excel del cliente aunque el orden
    --    de los sufijos parezca cruzado (Nucleo 2 -> '... NUCLEO 3', Nucleo 3 -> '... NUCLEO 2').
    -- =================================================================================
    INSERT INTO public.nucleos
           (nucleo_id, granja_id, nucleo_nombre, company_id, created_by_user_id, created_at,
            codigo_bodega, descripcion_bodega)
    SELECT v.nucleo_id, v_farm_id, v.nombre, v_company_id, 1, now(), v.cod_bodega, v.desc_bodega
    FROM (VALUES
        ('910001', 'Núcleo 1', 'B3001', 'BUG SUMINISTROS NUCLEO 1'),
        ('910002', 'Núcleo 2', 'B3002', 'BUG SUMINISTROS NUCLEO 3'),
        ('910003', 'Núcleo 3', 'B3003', 'BUG SUMINISTROS NUCLEO 2')
    ) AS v(nucleo_id, nombre, cod_bodega, desc_bodega)
    WHERE NOT EXISTS (SELECT 1 FROM public.nucleos n
                       WHERE n.nucleo_id = v.nucleo_id AND n.granja_id = v_farm_id);

    -- =================================================================================
    -- 8) GALPONES — 38 (tipo 'Jaula'), datos VERBATIM del Excel.
    --    galpon_id: la PK es global y con formato 'G0496'; se derivan del maximo vigente
    --    ('G' || lpad(max + fila, 4, '0')) — NUNCA se hardcodean, y la idempotencia se
    --    apoya en (granja_id, galpon_nombre), no en el id.
    --    Reparto a nucleos = DATO FICTICIO documentado (el Excel no asigna galpon->nucleo):
    --    Galpon 1-13 -> 910001, 14-26 -> 910002, 27-38 -> 910003. Ajustable luego en la UI.
    -- =================================================================================
    INSERT INTO public.galpones
           (galpon_id, nucleo_id, granja_id, galpon_nombre, tipo_galpon,
            company_id, created_by_user_id, created_at,
            codigo_erp_ubicacion, descripcion_erp_ubicacion)
    SELECT 'G' || lpad((b.max_num + n.rn)::text, 4, '0'),
           n.nucleo_id, v_farm_id, n.nombre, n.tipo,
           v_company_id, 1, now(), n.cod_erp, n.desc_erp
    FROM (
        SELECT s.*, row_number() OVER (ORDER BY s.orden) AS rn
        FROM (
            SELECT v.orden, v.nombre, v.cod_erp, v.desc_erp, v.tipo,
                   CASE WHEN v.orden <= 13 THEN '910001'
                        WHEN v.orden <= 26 THEN '910002'
                        ELSE '910003' END AS nucleo_id
            FROM (VALUES
        (1, 'Galpón 1', 'BG60201', 'BUG GR. ESPERANZA GALPON 1', 'Jaula'),
        (2, 'Galpón 2', 'BG60202', 'BUG GR. ESPERANZA GALPON 2', 'Jaula'),
        (3, 'Galpón 3', 'BG60203', 'BUG GR. ESPERANZA GALPON 3', 'Jaula'),
        (4, 'Galpón 4', 'BG60204', 'BUG GR. ESPERANZA GALPON 4', 'Jaula'),
        (5, 'Galpón 5', 'BG60205', 'BUG GR. ESPERANZA GALPON 5', 'Jaula'),
        (6, 'Galpón 6', 'BG60206', 'BUG GR. ESPERANZA GALPON 6', 'Jaula'),
        (7, 'Galpón 7', 'BG60207', 'BUG GR. ESPERANZA GALPON 7', 'Jaula'),
        (8, 'Galpón 8', 'BG60208', 'BUG GR. ESPERANZA GALPON 8', 'Jaula'),
        (9, 'Galpón 9', 'BG60209', 'BUG GR. ESPERANZA GALPON 9', 'Jaula'),
        (10, 'Galpón 10', 'BG60210', 'BUG GR. ESPERANZA GALPON 10', 'Jaula'),
        (11, 'Galpón 11', 'BG60211', 'BUG GR. ESPERANZA GALPON 11', 'Jaula'),
        (12, 'Galpón 12', 'BG60212', 'BUG GR. ESPERANZA GALPON 12', 'Jaula'),
        (13, 'Galpón 13', 'BG60213', 'BUG GR. ESPERANZA GALPON 13', 'Jaula'),
        (14, 'Galpón 14', 'BG60214', 'BUG GR. ESPERANZA GALPON 14', 'Jaula'),
        (15, 'Galpón 15', 'BG60215', 'BUG GR. ESPERANZA GALPON 15', 'Jaula'),
        (16, 'Galpón 16', 'BG60216', 'BUG GR. ESPERANZA GALPON 16', 'Jaula'),
        (17, 'Galpón 17', 'BG60217', 'BUG GR. ESPERANZA GALPON 17', 'Jaula'),
        (18, 'Galpón 18', 'BG60218', 'BUG GR. ESPERANZA GALPON 18', 'Jaula'),
        (19, 'Galpón 19', 'BG60219', 'BUG GR. ESPERANZA GALPON 19', 'Jaula'),
        (20, 'Galpón 20', 'BG60220', 'BUG GR. ESPERANZA GALPON 20', 'Jaula'),
        (21, 'Galpón 21', 'BG60221', 'BUG GR. ESPERANZA GALPON 21', 'Jaula'),
        (22, 'Galpón 22', 'BG60222', 'BUG GR. ESPERANZA GALPON 22', 'Jaula'),
        (23, 'Galpón 23', 'BG60223', 'BUG GR. ESPERANZA GALPON 23', 'Jaula'),
        (24, 'Galpón 24', 'BG60224', 'BUG GR. ESPERANZA GALPON 24', 'Jaula'),
        (25, 'Galpón 25', 'BG60225', 'BUG GR. ESPERANZA GALPON 25', 'Jaula'),
        (26, 'Galpón 26', 'BG60226', 'BUG GR. ESPERANZA GALPON 26', 'Jaula'),
        (27, 'Galpón 27', 'BG60227', 'BUG GR. ESPERANZA GALPON 27', 'Jaula'),
        (28, 'Galpón 28', 'BG60228', 'BUG GR. ESPERANZA GALPON 28', 'Jaula'),
        (29, 'Galpón 29', 'BG60229', 'BUG GR. ESPERANZA GALPON 29', 'Jaula'),
        (30, 'Galpón 30', 'BG60230', 'BUG GR. ESPERANZA GALPON 30', 'Jaula'),
        (31, 'Galpón 31', 'BG60231', 'BUG GR. ESPERANZA GALPON 31', 'Jaula'),
        (32, 'Galpón 32', 'BG60232', 'BUG GR. ESPERANZA GALPON 32', 'Jaula'),
        (33, 'Galpón 33', 'BG60233', 'BUG GR. ESPERANZA GALPON 33', 'Jaula'),
        (34, 'Galpón 34', 'BG60234', 'BUG GR. ESPERANZA GALPON 34', 'Jaula'),
        (35, 'Galpón 35', 'BG60235', 'BUG GR. ESPERANZA GALPON 35', 'Jaula'),
        (36, 'Galpón 36', 'BG60236', 'BUG GR. ESPERANZA GALPON 36', 'Jaula'),
        (37, 'Galpón 37', 'BG60237', 'BUG GR. ESPERANZA GALPON 37', 'Jaula'),
        (38, 'Galpón 38', 'BG60238', 'BUG GR. ESPERANZA GALPON 38', 'Jaula')
            ) AS v(orden, nombre, cod_erp, desc_erp, tipo)
        ) s
        WHERE NOT EXISTS (SELECT 1 FROM public.galpones g
                           WHERE g.granja_id = v_farm_id AND g.galpon_nombre = s.nombre)
    ) n
    CROSS JOIN (
        SELECT COALESCE(MAX(CAST(SUBSTRING(g.galpon_id FROM 2) AS integer)), 0) AS max_num
        FROM public.galpones g
        WHERE g.galpon_id ~ '^G[0-9]+$'
    ) b;

    -- =================================================================================
    -- 9) SILOS / BODEGA DE INSUMOS — 38 silos + 1 bodega, a farm_silos.
    --    NO son galpones a proposito: no deben aparecer al crear lotes.
    -- =================================================================================
    INSERT INTO public.farm_silos
           (company_id, granja_id, nombre, tipo, codigo_erp_ubicacion, descripcion,
            centro_operacion, codigo_bodega, activo, created_at)
    SELECT v_company_id, v_farm_id, v.nombre, v.tipo, v.cod_erp, v.descripcion,
           v.centro_operacion, v.cod_bodega, true, now()
    FROM (VALUES
        ('Silo 1', 'Silo', 'BS60101', 'BUG GR. ESPERANZA SILO 1', '830', 'B0601'),
        ('Silo 2', 'Silo', 'BS60102', 'BUG GR. ESPERANZA SILO 2', '830', 'B0601'),
        ('Silo 3', 'Silo', 'BS60103', 'BUG GR. ESPERANZA SILO 3', '830', 'B0601'),
        ('Silo 4', 'Silo', 'BS60104', 'BUG GR. ESPERANZA SILO 4', '830', 'B0601'),
        ('Silo 5', 'Silo', 'BS60105', 'BUG GR. ESPERANZA SILO 5', '830', 'B0601'),
        ('Silo 6', 'Silo', 'BS60106', 'BUG GR. ESPERANZA SILO 6', '830', 'B0601'),
        ('Silo 7', 'Silo', 'BS60107', 'BUG GR. ESPERANZA SILO 7', '830', 'B0601'),
        ('Silo 8', 'Silo', 'BS60108', 'BUG GR. ESPERANZA SILO 8', '830', 'B0601'),
        ('Silo 9', 'Silo', 'BS60109', 'BUG GR. ESPERANZA SILO 9', '830', 'B0601'),
        ('Silo 10', 'Silo', 'BS60110', 'BUG GR. ESPERANZA SILO 10', '830', 'B0601'),
        ('Silo 11', 'Silo', 'BS60111', 'BUG GR. ESPERANZA SILO 11', '830', 'B0601'),
        ('Silo 12', 'Silo', 'BS60112', 'BUG GR. ESPERANZA SILO 12', '830', 'B0601'),
        ('Silo 13', 'Silo', 'BS60113', 'BUG GR. ESPERANZA SILO 13', '830', 'B0601'),
        ('Silo 14', 'Silo', 'BS60114', 'BUG GR. ESPERANZA SILO 14', '830', 'B0601'),
        ('Silo 15', 'Silo', 'BS60115', 'BUG GR. ESPERANZA SILO 15', '830', 'B0601'),
        ('Silo 16', 'Silo', 'BS60116', 'BUG GR. ESPERANZA SILO 16', '830', 'B0601'),
        ('Silo 17', 'Silo', 'BS60117', 'BUG GR. ESPERANZA SILO 17', '830', 'B0601'),
        ('Silo 18', 'Silo', 'BS60118', 'BUG GR. ESPERANZA SILO 18', '830', 'B0601'),
        ('Silo 19', 'Silo', 'BS60119', 'BUG GR. ESPERANZA SILO 19', '830', 'B0601'),
        ('Silo 20', 'Silo', 'BS60120', 'BUG GR. ESPERANZA SILO 20', '830', 'B0601'),
        ('Silo 21', 'Silo', 'BS60121', 'BUG GR. ESPERANZA SILO 21', '830', 'B0601'),
        ('Silo 22', 'Silo', 'BS60122', 'BUG GR. ESPERANZA SILO 22', '830', 'B0601'),
        ('Silo 23', 'Silo', 'BS60123', 'BUG GR. ESPERANZA SILO 23', '830', 'B0601'),
        ('Silo 24', 'Silo', 'BS60124', 'BUG GR. ESPERANZA SILO 24', '830', 'B0601'),
        ('Silo 25', 'Silo', 'BS60125', 'BUG GR. ESPERANZA SILO 25', '830', 'B0601'),
        ('Silo 26', 'Silo', 'BS60126', 'BUG GR. ESPERANZA SILO 26', '830', 'B0601'),
        ('Silo 27', 'Silo', 'BS60127', 'BUG GR. ESPERANZA SILO 27', '830', 'B0601'),
        ('Silo 28', 'Silo', 'BS60128', 'BUG GR. ESPERANZA SILO 28', '830', 'B0601'),
        ('Silo 29', 'Silo', 'BS60129', 'BUG GR. ESPERANZA SILO 29', '830', 'B0601'),
        ('Silo 30', 'Silo', 'BS60130', 'BUG GR. ESPERANZA SILO 30', '830', 'B0601'),
        ('Silo 31', 'Silo', 'BS60131', 'BUG GR. ESPERANZA SILO 31', '830', 'B0601'),
        ('Silo 32', 'Silo', 'BS60132', 'BUG GR. ESPERANZA SILO 32', '830', 'B0601'),
        ('Silo 33', 'Silo', 'BS60133', 'BUG GR. ESPERANZA SILO 33', '830', 'B0601'),
        ('Silo 34', 'Silo', 'BS60134', 'BUG GR. ESPERANZA SILO 34', '830', 'B0601'),
        ('Silo 35', 'Silo', 'BS60135', 'BUG GR. ESPERANZA SILO 35', '830', 'B0601'),
        ('Silo 36', 'Silo', 'BS60136', 'BUG GR. ESPERANZA SILO 36', '830', 'B0601'),
        ('Silo 37', 'Silo', 'BS60137', 'BUG GR. ESPERANZA SILO 37', '830', 'B0601'),
        ('Silo 38', 'Silo', 'BS60138', 'BUG GR. ESPERANZA SILO 38', '830', 'B0601'),
        ('Insumos', 'Insumos', 'BUG60100', 'BUG. PRINCIPAL GR. ESPERANZA', '830', 'B0601')
    ) AS v(nombre, tipo, cod_erp, descripcion, centro_operacion, cod_bodega)
    WHERE NOT EXISTS (SELECT 1 FROM public.farm_silos s
                       WHERE s.granja_id = v_farm_id AND s.nombre = v.nombre);

    -- =================================================================================
    -- 10) USER_FARMS — ambos usuarios sobre La Esperanza (admin + por defecto).
    --     created_by_user_id es UUID: se usa el uuid del propio usuario.
    -- =================================================================================
    INSERT INTO public.user_farms (user_id, farm_id, is_admin, is_default, created_at, created_by_user_id)
    SELECT u.id, v_farm_id, true, true, now(), u.id
    FROM (VALUES (v_user_admin_id), (v_user_impl_id)) AS u(id)
    WHERE NOT EXISTS (SELECT 1 FROM public.user_farms uf
                       WHERE uf.user_id = u.id AND uf.farm_id = v_farm_id);

    -- =================================================================================
    -- 11) CATALOGO DE ITEMS — 310 items (45 alimento / 244 insumos / 21 huevo).
    --     item_type se mapea de la categoria del Excel; metadata jsonb solo con las
    --     claves que traen valor (categoria siempre; referencia/tipoInventario/um/tipoHuevo
    --     cuando vienen). Idempotencia por (company_id, codigo).
    -- =================================================================================
    INSERT INTO public.catalogo_items
           (codigo, nombre, metadata, activo, created_at, updated_at, company_id, pais_id, item_type)
    SELECT v.codigo,
           v.descripcion,
           jsonb_build_object('categoria', v.categoria)
             || CASE WHEN v.referencia      IS NOT NULL THEN jsonb_build_object('referencia', v.referencia)          ELSE '{}'::jsonb END
             || CASE WHEN v.tipo_inventario IS NOT NULL THEN jsonb_build_object('tipoInventario', v.tipo_inventario) ELSE '{}'::jsonb END
             || CASE WHEN v.um              IS NOT NULL THEN jsonb_build_object('um', v.um)                          ELSE '{}'::jsonb END
             || CASE WHEN v.tipo_huevo      IS NOT NULL THEN jsonb_build_object('tipoHuevo', v.tipo_huevo)           ELSE '{}'::jsonb END,
           true, now(), now(), v_company_id, v_pais_id,
           CASE v.categoria
                WHEN 'ALIMENTO'                        THEN 'alimento'
                WHEN 'HUEVO'                           THEN 'huevo'
                WHEN 'VACUNAS'                         THEN 'vacuna'
                WHEN 'MEDICAMENTOS'                    THEN 'medicamento'
                WHEN 'DESINFECTANTES'                  THEN 'desinfectante'
                WHEN 'COMBUSTIBLES'                    THEN 'combustible'
                WHEN 'EMPAQUES'                        THEN 'empaque'
                WHEN 'INSUMOS'                         THEN 'insumo'
                WHEN 'MANTENIMIENTO Y REPARAC MAQ Y EQ' THEN 'mantenimiento'
                WHEN 'MATERIAS PRIMAS'                 THEN 'materia_prima'
                WHEN 'PRODUCTO TERMINADO'              THEN 'producto_terminado'
                ELSE 'insumo'   -- inalcanzable con estos datos (las 10 categorias estan mapeadas)
           END
    FROM (VALUES
        ('ALIMENTO', '1918', '1005', 'POLLITAS PREINICIADOR STA RITA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1917', '1010', 'POLLITAS INICIACION STA RITA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2002', '1015', 'POLLAS CRECIMIENTO STA RITA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1987', '1020', 'POLLAS LEVANTE STA RITA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2243', '1030', 'PREPICO INICIAL STA RITA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2227', '1035', 'PREPICO ARRANQUE STA RITA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2237', '1040', 'PREPICO 100 STA RITA JAULA AC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2229', '1042', 'PREPICO 100 STA RITA PISO AC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2240', '1050', 'SUPER HUEVO PREPICO STA RITA JAULA AC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2249', '1052', 'SUPER HUEVO PREPICO STA RITA PISO AC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2248', '1059', 'HUEVO PREPICO STA RITA BC DORADO CRIOLLA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2232', '1060', 'HUEVO FASE II STA RITA JAULA AC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2233', '1062', 'HUEVO FASE II STA RITA PISO AC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2786', '1082', 'MQ PREPICO ARRANQUE SR SIN BETAINA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2787', '1085', 'MQ PREPICO ARRANQUE SR INTELLA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5353', '1091', 'MQ ENS PREPICO 100 TT2 POUL HIGH C + 50', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5354', '1092', 'MQ ENS PREPICO 100 TT3 POUL LOW C + 50', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5355', '1093', 'MQ ENS PREPICO 100 TT4 LAY CONT 2830', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5356', '1094', 'MQ ENS PREPICO 100 TT5 LAY HIGH LC + 60', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5357', '1095', 'MQ ENS PREPICO 100 TT6 LAY LOW LC-60', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5352', '1096', 'MQ ENS PREPICO 100 ST RITA JAULA SN BTNA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5358', '1097', 'MQ ENS PREPICO 100 ST RITA JAULA INTELLA', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1317', '2500', 'POLLITO PREINICIADOR Q', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1319', '2794', 'POLLITO PREINICIADOR Q SIN COCC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '2750', '3503', 'POLLITA PREINICIADOR Q SIN ANTICOCCIDIAL', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1254', '4361', 'POLLITA PREINICIADOR SR Q SIN COCC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1260', '4365', 'POLLITA INICIACION SR Q SIN COCC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1261', '4366', 'GR POLLITA INICIACION SR Q SIN COCC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1268', '4369', 'POLLA CRECIMIENTO SR H SIN COCC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1270', '4373', 'POLLA LEVANTE SR H SIN COCC', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1284', '4375', 'PREPICO ARRANQUE SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1285', '4376', 'GR PREPICO ARRANQUE SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1294', '4377', 'PREPICO 100 SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1295', '4378', 'GR PREPICO 100 SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1297', '4380', 'GR PREPICO 100 SR Q', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1302', '4385', 'PREPICO 100 BONEGG SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1304', '4387', 'PREPICO 100 OMEGA SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1307', '4389', 'CAMPESINO SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1308', '4390', 'GR CAMPESINO SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1310', '4392', 'GR SUPER HUEVO PREPICO SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1313', '4395', 'HUEVO FASE II SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1314', '4396', 'GR HUEVO FASE II SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1281', '4428', 'PREPICO INICIAL SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '1283', '4430', 'GR PREPICO INICIAL SR H', 'IN300511S', NULL, NULL),
        ('ALIMENTO', '5359', '4499', 'MQ ENS PREPICO 100 TT1 POUL CONT 2770', 'IN300511S', NULL, NULL),
        ('COMBUSTIBLES', '1205', NULL, 'GAS X KILO', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1916', NULL, 'ACEITE PARA MOTOR DE 2 TIEMPOS', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1901', NULL, 'GASOLINA CORRIENTE', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1951', NULL, 'A.C.P.M X GALON EXC. IVA', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1973', NULL, 'GRASA PARA GUADAÑA/LITIO #2', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1509', NULL, 'ACEITE HIDRAULICO IBC ISO-68 GARR.X 5 GL', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1953', NULL, 'ACEITE PREMIUM REDUCTOR F3651-460 X5GLN', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1950', NULL, 'A.C.P.M X GALON IVA DEL 5%', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1891', NULL, 'ACEITE PENET.EXTRAPREMIUM F3935-SP*20ONZ', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1949', NULL, 'A.C.P.M X GALON IVA DEL 19%', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1854', NULL, 'ACEITE AEROSOL MULTIUSOS WD-40 B.X 11ONZ', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1856', NULL, 'ACEITE MOTOR MOBIL 2 TIEMPOS BOT.X 946ML', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1857', NULL, 'ACEITE MOTOR MOBIL 20W-50 G.X 3.785 ML', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1855', NULL, 'ACEITE MOTOR MOBIL LUB.15W-40 G.X3.785', NULL, NULL, NULL),
        ('COMBUSTIBLES', '1510', NULL, 'ACEITE MOTOR HAVOLINE SAE 40 G.X 3.785ML', NULL, NULL, NULL),
        ('DESINFECTANTES', '1241', NULL, 'POLYQUAT 450 (DESINFECTANTE) X 5 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '785', NULL, 'AVIYODOX GALON X 20 LITROS', NULL, NULL, NULL),
        ('DESINFECTANTES', '1476', NULL, 'SANITAS WP X 5 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1914', NULL, 'YODO AL 22,5% GFA X 19 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '2477', NULL, 'TH4 (DESINFECTANTE) X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '2466', NULL, 'VELA FUMAGRI HA X 16G', NULL, NULL, NULL),
        ('DESINFECTANTES', '2465', NULL, 'PARAQUAT ALEMAN 20 SL (HERBICIDA) X 1 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1896', NULL, 'BIOSAFE-GT (FUNGICIDA) X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1970', NULL, 'ECOREX AQUAFLOW SE (INSECTICIDA) X 1 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1895', NULL, 'BENTONITA SACO X 50 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '2457', NULL, 'MEVIPOW DESINFECTANTE X 1 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '1994', NULL, 'VIRUCIDA POLVO VIRKON S X 10 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '2471', NULL, 'VIROSHIELD DESINFECTANTE X 5 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1967', NULL, 'DETIA GAS X 500 TABLETAS', NULL, NULL, NULL),
        ('DESINFECTANTES', '1963', NULL, 'INSECTICIDA L.CIPERMETRINA 15% FCO.X 1LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '2476', NULL, 'VIROSHIELD DESINFECTANTE X 25 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '2479', NULL, 'RATICIDA RATAQUILL GEL BOLSA  * 100 GRS', NULL, NULL, NULL),
        ('DESINFECTANTES', '2480', NULL, 'SNIPER SC 10% (INSECTICIDA) X 1 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '789', NULL, 'HYPEROX GAL X 5LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '772', NULL, 'VIROCID (DESINFECTANTE) X 5 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '774', NULL, 'VIRUKILL GFA X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '775', NULL, 'FUNGHUMOX (FUNGICIDA) TARRO X 25 GR', NULL, NULL, NULL),
        ('DESINFECTANTES', '1988', NULL, 'RATUNET BLOQUES X 5 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '1236', NULL, 'GLH 20 (GLUTARALDEHIDO 20%) X 3,8 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '788', NULL, 'BIODES 200 GFA X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '790', NULL, 'CAL VIVA MOLIDA AL 90% SACO X 25 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '799', NULL, 'NAT BIO KONTRA X 4 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '800', NULL, 'DETERGENTE LIQUIDO CLEANBAC X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '801', NULL, 'DESINFECTANTE AQ-B GALON X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '802', NULL, 'DESINFECTANTE AQ-DEX GALON X 4 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1906', NULL, 'OXCIP (DESINFECTANTE) GALON X 4 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1239', NULL, 'DETERGENTE ALCALINO CLORADO X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '786', NULL, 'ALCOHOL ETILICO ANTISEP.70% BOT.X 1 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1909', NULL, 'JABON DUCHAS VANTOCLEAN X 4 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1955', NULL, 'ALCOHOL ANTISEPTICO AL 70% X 3,78 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '803', NULL, 'DESINFECTANTE OXCIP GALON X 4,0 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1960', NULL, 'BIOTRAMPA (CONTROL MOSCA) X 4 LTS', NULL, NULL, NULL),
        ('DESINFECTANTES', '1892', NULL, 'ACIDO ACETICO 20% GFA X 20 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '1248', NULL, 'ECOSOAP-BAC (JABON ANTIBACTERIAL) X 4KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '1969', NULL, 'ECODES BM GFA X 20 LITROS', NULL, NULL, NULL),
        ('DESINFECTANTES', '1246', NULL, 'Q-NEST (DESINFECTANTE) SACO X 25 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '792', NULL, 'FORMOL AL 37% GFA X 30 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '794', NULL, 'DETERGENTE DETERCLOR GALON X 3.8 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '797', NULL, 'DERMIGEL (GEL ANTIBACTERIAL) X 4 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '2600', NULL, 'ALCOHOL ANTISEPTICO AL 70% X 1 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '793', NULL, 'DETERGENTE AV 21 B GALON X 3,8 LT', NULL, NULL, NULL),
        ('DESINFECTANTES', '796', NULL, 'DESINFECTANTE ANFOCUAT GALON X 3,8 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '787', NULL, 'WHISPER V (DESINFECTANTE) X 4 KG', NULL, NULL, NULL),
        ('DESINFECTANTES', '1477', NULL, 'DETERGENTE ACIDO DES OX GFA X 20 LT', NULL, NULL, NULL),
        ('EMPAQUES', '1208', NULL, 'PELICULA PLAST EXTENSIBLE ROLLO X 650 MT', NULL, NULL, NULL),
        ('EMPAQUES', '700', NULL, 'BANDEJA CARTON HUEVOS OCRE T AA X 30 UN', NULL, NULL, NULL),
        ('EMPAQUES', '706', NULL, 'BANDEJA CARTON HUEVOS GRIS X 30 UN', NULL, NULL, NULL),
        ('EMPAQUES', '2446', NULL, 'LAMINA CARTON CORRUGADO 120 X 90 C930K', NULL, NULL, NULL),
        ('EMPAQUES', '2408', NULL, 'EMPAQUE DE POLIPROPILENO', NULL, NULL, NULL),
        ('EMPAQUES', '2381', NULL, 'BANDEJA CARTON HUEVOS FUSCIA X 30 UN', NULL, NULL, NULL),
        ('EMPAQUES', '2390', NULL, 'STICKER 32*25 BLANCO TT(18000)', NULL, NULL, NULL),
        ('EMPAQUES', '615', NULL, 'LAMINA CARTON CORR ESQUINERO 100X30 CM', NULL, NULL, NULL),
        ('EMPAQUES', '643', NULL, 'CINTA ADH TRANSP DE 5 CM ROLLO X 100 MT', NULL, NULL, NULL),
        ('EMPAQUES', '644', NULL, 'PITA PLASTICA ROJA ROLLO X 750 MT', NULL, NULL, NULL),
        ('EMPAQUES', '623', NULL, 'BOLSA PLAST TRANSP GRANJAS 55X60 CM', NULL, NULL, NULL),
        ('EMPAQUES', '646', NULL, 'HILO POLIESTER P COSEDORA FISCHBEN X 2KG', NULL, NULL, NULL),
        ('EMPAQUES', '647', NULL, 'ETIQUETA T ADH BLANCA 5X2,5CM X5000UN', NULL, NULL, NULL),
        ('EMPAQUES', '702', NULL, 'BANDEJA CARTON HUEVOS VERDE T AA X 30 UN', NULL, NULL, NULL),
        ('EMPAQUES', '678', NULL, 'CINTA T TERMICA CERA NEGRA ROLLO X 300MT', NULL, NULL, NULL),
        ('EMPAQUES', '709', NULL, 'BANDEJA CARTON HUEVOS AMARILLA X 30 UN', NULL, NULL, NULL),
        ('EMPAQUES', '625', NULL, 'CANASTILLA PLAST CERRADA GRIS 60X40X40CM', NULL, NULL, NULL),
        ('EMPAQUES', '670', NULL, 'LONAS FIBRA BLANCA DE SEGUNDA 100X60 CM', NULL, NULL, NULL),
        ('EMPAQUES', '703', NULL, 'BANDEJA CARTON HUEVOS AZUL X 30 UN', NULL, NULL, NULL),
        ('EMPAQUES', '677', NULL, 'ETIQUETA T ADH ROJA 5X2,5CM X5000UN', NULL, NULL, NULL),
        ('EMPAQUES', '708', NULL, 'BANDEJA CARTON HUEVOS ROJA X 30 UN', NULL, NULL, NULL),
        ('INSUMOS', '934', NULL, 'CLORO PASTILLAS 91% SIN IVA X KG', NULL, NULL, NULL),
        ('INSUMOS', '863', NULL, 'ROUNDUP ACTIVO LIQ GAL X 4 LT', NULL, NULL, NULL),
        ('INSUMOS', '874', NULL, 'CIPERMETRINA 15% X 1 LT', NULL, NULL, NULL),
        ('INSUMOS', '880', NULL, 'VETANCID POLVO X KG (BOLSAX15KG)', NULL, NULL, NULL),
        ('INSUMOS', '1472', NULL, 'AGRISTERYL (DESINFECTANTE) GFA X 20 LT', NULL, NULL, NULL),
        ('INSUMOS', '867', NULL, 'RATUNET PELLETS X 5 KG', NULL, NULL, NULL),
        ('INSUMOS', '2533', NULL, 'FERTILIZANTE GRANULADO UREA BTO X 50 KL', NULL, NULL, NULL),
        ('INSUMOS', '1956', NULL, 'PROFIAMINA 480 SL (HERBICIDA) X 1 LT', NULL, NULL, NULL),
        ('INSUMOS', '2587', NULL, 'ABONO TRIPLE 15-15-15 SACO X 50 KG', NULL, NULL, NULL),
        ('INSUMOS', '2534', NULL, 'GLIFOSOL 480 SL (HERBICIDA) X 1 LT', NULL, NULL, NULL),
        ('INSUMOS', '1989', NULL, 'SAL HIDROSAL INDUSTRIAL 99% SACO X 25 KG', NULL, NULL, NULL),
        ('INSUMOS', '2530', NULL, 'ESPECIFICO AL 20% X LITRO', NULL, NULL, NULL),
        ('INSUMOS', '1911', NULL, 'VENOCLISES (MACROGOTEO SENCILLO)', NULL, NULL, NULL),
        ('INSUMOS', '2559', NULL, 'FUMI-GATHE X LT (CONTROL MOSCA)', NULL, NULL, NULL),
        ('INSUMOS', '2529', NULL, 'SULFEX COBRE X 1 KG', NULL, NULL, NULL),
        ('INSUMOS', '1200', NULL, 'EM AGRO (FERTILIZANTE) X LITRO', NULL, NULL, NULL),
        ('INSUMOS', '868', NULL, 'NUVAN 50 EC (INSECTICIDA) X 1LT', NULL, NULL, NULL),
        ('INSUMOS', '869', NULL, 'RATICIDA ANTIC.RATUNET GRA.BLOQUES X 5KG', NULL, NULL, NULL),
        ('INSUMOS', '870', NULL, 'CATCHMASTER (CONTROL MOSCA) 9MTX28CM UND', NULL, NULL, NULL),
        ('INSUMOS', '881', NULL, 'RASTOP PASTA (RATICIDA) X 1 KG', NULL, NULL, NULL),
        ('INSUMOS', '1196', NULL, 'GAS X CILINDRO DE 100 LBS', NULL, NULL, NULL),
        ('INSUMOS', '884', NULL, 'KLERAT CEBO BLOQUE (RATICIDA) X 1 KG', NULL, NULL, NULL),
        ('INSUMOS', '871', NULL, 'NEO SERPA RAT CEBO (RATICIDA) X 1 KG', NULL, NULL, NULL),
        ('INSUMOS', '1202', NULL, 'SAL COMUN X KILO', NULL, NULL, NULL),
        ('INSUMOS', '885', NULL, 'CERTUS 70 WS (INSECTICIDA) BOLSA X 500GR', NULL, NULL, NULL),
        ('INSUMOS', '2014', NULL, 'CALFOSVIT SE L (SUPLEMENTO) X FCO 500ML', NULL, NULL, NULL),
        ('INSUMOS', '939', NULL, 'CARBON ANTRACITA MINERAL SACO X 50KG', NULL, NULL, NULL),
        ('INSUMOS', '864', NULL, 'GRAMOXONE (HERBICIDA) X 1 LT', NULL, NULL, NULL),
        ('INSUMOS', '866', NULL, 'DELMETRIN 2,5% EC (INSECTICIDA) X 1 LT', NULL, NULL, NULL),
        ('INSUMOS', '954', NULL, 'BIOCALCIO L PN 5% GARRAFA X 22 LT', NULL, NULL, NULL),
        ('INSUMOS', '943', NULL, 'ACIDIFICANTE CLAP PRO X 23 LT 19%', NULL, NULL, NULL),
        ('INSUMOS', '930', NULL, 'REACTIVO COLORIMETRIA HIERRO LIBRE X UND', NULL, NULL, NULL),
        ('INSUMOS', '933', NULL, 'REACTIVO COLORIMETRIA CLORO LIBRE X UND', NULL, NULL, NULL),
        ('INSUMOS', '924', NULL, 'HIPOCLORITO DE SODIO AL 13% X 25 KG', NULL, NULL, NULL),
        ('INSUMOS', '1194', NULL, 'CASCARILLA DE ARROZ X PACA', NULL, NULL, NULL),
        ('INSUMOS', '1964', NULL, 'CLORO GRANULADO 70% CÑETE X 45 KG', NULL, NULL, NULL),
        ('INSUMOS', '2811', NULL, 'ASATECH SOBRE X 500 GR', NULL, NULL, NULL),
        ('INSUMOS', '1915', NULL, 'CASCARILLA DE ARROZ CRUDA PACA', NULL, NULL, NULL),
        ('INSUMOS', '923', NULL, 'COAYUDANTE ANIONICO REF SQ041 X KG', NULL, NULL, NULL),
        ('INSUMOS', '928', NULL, 'COAGULANTE ALUMINIO LIQ GFA X 25 LT', NULL, NULL, NULL),
        ('INSUMOS', '955', NULL, 'CALBIOTECH X 1 LT IVA  5%', NULL, NULL, NULL),
        ('INSUMOS', '938', NULL, 'ACONDICIONADOR PH LIQ GFA X 25 LT', NULL, NULL, NULL),
        ('INSUMOS', '931', NULL, 'LIMPIADOR ACIDO NITRICO GFA X 25 LT', NULL, NULL, NULL),
        ('INSUMOS', '932', NULL, 'CITROQUIM AQUA GFA X 20 LT', NULL, NULL, NULL),
        ('INSUMOS', '1474', NULL, 'CARBONATO DE CALCIO MALLA 6 SACO X 40 KG', NULL, NULL, NULL),
        ('INSUMOS', '951', NULL, 'ACTIVATECH X SOBRE DE 250 GR 5%', NULL, NULL, NULL),
        ('INSUMOS', '946', NULL, 'POLIMEVE PROBIOTICO SOBRE X 100 GR', NULL, NULL, NULL),
        ('INSUMOS', '1473', NULL, 'CINTAS DE AMONIACO X UND', NULL, NULL, NULL),
        ('INSUMOS', '1471', NULL, 'NEUTRALIZANTE OLOR FRUTAL X 4 LT', NULL, NULL, NULL),
        ('INSUMOS', '1727', NULL, 'ASERRIN SACO X 10 KILOS', NULL, NULL, NULL),
        ('INSUMOS', '2678', NULL, 'LIPOFEED (SUPLEMENTO) GFA X 20 LT', NULL, NULL, NULL),
        ('INSUMOS', '2668', NULL, 'COAGULANTE MACKENFLOC II LIQ GFA X 25 LT', NULL, NULL, NULL),
        ('INSUMOS', '940', NULL, 'ARENA CUARZO CRISTALIZADA SACO X 50 KG', NULL, NULL, NULL),
        ('INSUMOS', '941', NULL, 'GRAVA DE CUARZO SACO X 50 KG', NULL, NULL, NULL),
        ('INSUMOS', '947', NULL, 'SUPLEMENTO VIT.CALFOSVIT SE L.FCO.X500ML', NULL, NULL, NULL),
        ('INSUMOS', '920', NULL, 'SOLUCION A.ELECTRODOS HI70300L X 500 ML', NULL, NULL, NULL),
        ('INSUMOS', '1468', NULL, 'ACTIVADOR ACI.ORGANICOS TTO AGUAS BIO EN', NULL, NULL, NULL),
        ('INSUMOS', '935', NULL, 'SOLUCION BUFFER HI7004 PH 4,01 X 1 LT', NULL, NULL, NULL),
        ('INSUMOS', '936', NULL, 'SOLUCION BUFFER HI5007 PH 7,01 X 500 ML', NULL, NULL, NULL),
        ('INSUMOS', '926', NULL, 'SOLUCION PRUEBA ORP HI7022 L X 500 ML', NULL, NULL, NULL),
        ('MANTENIMIENTO Y REPARAC MAQ Y EQ', '2020', NULL, 'CADENA COMEDERO CHAMPION S.ALIMENTACION', NULL, NULL, NULL),
        ('MANTENIMIENTO Y REPARAC MAQ Y EQ', '1520', NULL, 'BANDA GALLINAZA RLL 1190MM*300MTS*1,1MM', NULL, NULL, NULL),
        ('MANTENIMIENTO Y REPARAC MAQ Y EQ', '1521', NULL, 'BANDA GALLINAZA RLL 1210MM*300MTS*1,2MM', NULL, NULL, NULL),
        ('MANTENIMIENTO Y REPARAC MAQ Y EQ', '1512', NULL, 'ACEITE MOTOR MOBIL 2 TIEMPOS BOT.X 946ML', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '1689', NULL, 'SAL MARINA INDUST. MOLIDA*50 KG', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '1706', NULL, 'CARBONATO DE CALCIO GRUESO R6', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '1979', NULL, 'MELAZA BTO X 30 KG', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '2197', NULL, 'SALVIX  NOTBIO PEOMITEC FITOGE', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '2181', NULL, 'BICARBONATO DE SODIO 99% Na27% IVA 19%', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '1675', NULL, 'DL METIONINA 88% LIQ', NULL, NULL, NULL),
        ('MATERIAS PRIMAS', '2223', NULL, 'AZUFRE SIN IVA SACO X 25 KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2747', NULL, 'COLOSTRUM BIO 21 X 50 ML', NULL, NULL, NULL),
        ('MEDICAMENTOS', '900', NULL, 'AGUA ESTERIL BOLSA X 500 ML', NULL, NULL, NULL),
        ('MEDICAMENTOS', '893', NULL, 'GRAM UP X KILO', NULL, NULL, NULL),
        ('MEDICAMENTOS', '906', NULL, 'VIRKON S BALDE DE 10KG (KG)', NULL, NULL, NULL),
        ('MEDICAMENTOS', '916', NULL, 'MUCOSOL SOBRE X 100G', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2503', NULL, 'BRONCOBIOL GFA X 20 LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '1984', NULL, 'NOVABRONCOL GFA X 20LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2511', NULL, 'INMUNAIR 17,5% FCO X 500ML', NULL, NULL, NULL),
        ('MEDICAMENTOS', '1893', NULL, 'ADOLEX POLVO SOLUBLE X 500 GR', NULL, NULL, NULL),
        ('MEDICAMENTOS', '1971', NULL, 'EXTIMOX 50% (AMOXICILINA) X 1 KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2490', NULL, 'GENTAX-100 (GENTAMICINA) FCO X 20ML', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2486', NULL, 'CIPROX AQUA 30% (CRIPROFLOXACINA) X KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '886', NULL, 'GENTAMICINA 10% FCO X 100 ML', NULL, NULL, NULL),
        ('MEDICAMENTOS', '915', NULL, 'MUCOLIPTOL GARRAFA X 20LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2519', NULL, 'TRIMEBAC (TRIMETOPRIM SULFA) X 1 LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '5335', NULL, 'TIA-DOX F+D+E(TIAMULINA+DOXICILINA) X KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '887', NULL, 'MIXILAND FM 4+4 (PENICILINA) FCO X 12 ML', NULL, NULL, NULL),
        ('MEDICAMENTOS', '918', NULL, 'SULFATO COBRE AZUL CRISTALES SACO X 25KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '1992', NULL, 'TIOSULFITO DE SODIO SACO X 25 KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2609', NULL, 'THIVET 50% (TIAMULINA) X 5KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '1996', NULL, 'VITALIUM GFA X 5 LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2500', NULL, 'REACTIVO ORTOTOLIDINA AMARILLA X 1 LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2504', NULL, 'PROLIV-TECH SOBRE X 300 GR', NULL, NULL, NULL),
        ('MEDICAMENTOS', '2621', NULL, 'SILYGRAN (HEPATOPROTECTOR) X 20 LT', NULL, NULL, NULL),
        ('MEDICAMENTOS', '912', NULL, 'ACETAMINOFEN TMB X 25 KG', NULL, NULL, NULL),
        ('MEDICAMENTOS', '917', NULL, 'PIGMENTO CANTAXANTINA 10% FCO X 50 GR', NULL, NULL, NULL),
        ('MEDICAMENTOS', '890', NULL, 'CAPISOLE (LEVAMISOL HCL 90%)SOBRE X 100G', NULL, NULL, NULL),
        ('VACUNAS', '808', NULL, 'NEW CASTLE +BRONQ MA5 CLONE 30 X2500', NULL, NULL, NULL),
        ('VACUNAS', '2806', NULL, 'VITABRON L X 2000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '851', NULL, 'DILUENTE OCULO NASAL X 2500 DS', NULL, NULL, NULL),
        ('VACUNAS', '856', NULL, 'GUMBORO CEPA SYSA 26 (NOVAMUNE)X 1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '835', NULL, 'BIOCOCCIVET (COCCIDIA) X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '839', NULL, 'SALMONELLA ENTERITIDIS VIVA X 2000 DS', NULL, NULL, NULL),
        ('VACUNAS', '1948', NULL, 'RISMAVAC X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '2579', NULL, 'CEVAMUNE PASTILLA NEUTRALIZANTE X 1 UND', NULL, NULL, NULL),
        ('VACUNAS', '1907', NULL, 'VACUNA V.AVIPRO SALM VAC E FCO X 2000 DS', NULL, NULL, NULL),
        ('VACUNAS', '855', NULL, 'NEUMOVIRUS MUERTA X 1000 HIPRA TRT', NULL, NULL, NULL),
        ('VACUNAS', '825', NULL, 'NEW CASTLE+BRONQU CLON/H120 X 2500 HIPRA', NULL, NULL, NULL),
        ('VACUNAS', '827', NULL, 'MIXIBAC HG(CORYZA-PASTERELLA) X 1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '846', NULL, 'VIRUELA AVIAR X 1000 DOSIS HIPRAPOX', NULL, NULL, NULL),
        ('VACUNAS', '842', NULL, 'NEW CASTLE+IB+EDS(TRIPLE PONEDORAS)X1000', NULL, NULL, NULL),
        ('VACUNAS', '1958', NULL, 'MICOPLASMA VIRUS VIVO CEPA F X 1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '1981', NULL, 'VACUNA V.MYCOPLA.GALLI.CEPA F FCOX1000DS', NULL, NULL, NULL),
        ('VACUNAS', '1904', NULL, 'HIPRAAVIAR CLON/H120 * 2500 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '806', NULL, 'VECTORMUNE HVT NDV X 2000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '849', NULL, 'NEW CASTLE+BRONQ  S/H 120 X 2500 HIPRA', NULL, NULL, NULL),
        ('VACUNAS', '1910', NULL, 'VECTORMUNE HVT LT X 2000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '2509', NULL, 'PRIMUN SALMONELLA E X 2000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '1961', NULL, 'MAREK RISPENS X 2000(CEVAC MD RISPENS)', NULL, NULL, NULL),
        ('VACUNAS', '1978', NULL, 'LARINGOTRAQUEITIS (LT IVAX) X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '1975', NULL, 'HIPRAVIAR SHS VIVA X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '1905', NULL, 'NOVAMUNE* 1000 DOSIS (GUMBORO)', NULL, NULL, NULL),
        ('VACUNAS', '2484', NULL, 'SALMONELLA BAC HIDRO ENTER+GALLIX1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '805', NULL, 'NDK BROILER X 5000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '1863', NULL, 'NEUTRALIZANTE NEUT 3D VACUNA 1LT SIN IVA', NULL, NULL, NULL),
        ('VACUNAS', '2743', NULL, 'INNOFUSION ND X 4000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '843', NULL, 'LARINGO VIRUELA (FP LT) X 2000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '840', NULL, 'CEVAC MG F (M.GALLISEPTICUM) X 1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '815', NULL, 'SALMONELLA ENTERITIDIS VIVA X 5000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '848', NULL, 'NEW CASTLE LASOTA X 2500 HIPRAVIAR S', NULL, NULL, NULL),
        ('VACUNAS', '852', NULL, 'SALMONELLA NOBILIS SG 9R X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '1983', NULL, 'VACUNA V.NOBILIS SG 9R FCO X 1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '1908', NULL, 'VACUNA V.AVIPRO SALM VAC E FCO X 5000 DS', NULL, NULL, NULL),
        ('VACUNAS', '826', NULL, 'DILUENTE DME DE VACUNA FCO X 60 CC', NULL, NULL, NULL),
        ('VACUNAS', '5390', NULL, 'COLERA AVIAR X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '5391', NULL, 'CORIMUNE (CORIZA INFECCIOSA) X 1000 DOSI', NULL, NULL, NULL),
        ('VACUNAS', '809', NULL, 'VACUNA V.HIPRAVIAR S FCO X 5000 DS', NULL, NULL, NULL),
        ('VACUNAS', '853', NULL, 'ENCEFALOVIRUELA X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '1741', NULL, 'ENCEFALOMIELITIS X 1000 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '824', NULL, 'AVISAN SECURE (SALMONELOSIS) X 1000 DS', NULL, NULL, NULL),
        ('VACUNAS', '1968', NULL, 'DILUENTE DE VACUNA X 2500 DOSIS', NULL, NULL, NULL),
        ('VACUNAS', '811', NULL, 'AGUJA DE 20G X 1/2 X UND', NULL, NULL, NULL),
        ('VACUNAS', '810', NULL, 'JERINGA DESECHABLE CON AGUJA DE 3 ML', NULL, NULL, NULL),
        ('VACUNAS', '812', NULL, 'AGUJA DE 20G X 1/4 X UND', NULL, NULL, NULL),
        ('VACUNAS', '804', NULL, 'KIT INYECTOR SOCOREX DE 0,5 ML', NULL, NULL, NULL),
        ('VACUNAS', '816', NULL, 'CILINDRO SOCOREX+EMBOLO DE 0,5 ML', NULL, NULL, NULL),
        ('VACUNAS', '822', NULL, 'VAC-PAC P TARRO X 100G', NULL, NULL, NULL),
        ('VACUNAS', '845', NULL, 'SPRAY VAC (ESTABILIZADOR VACUNA) X 1 LT', NULL, NULL, NULL),
        ('VACUNAS', '823', NULL, 'INYECTOR SOCOREX SENCILLO DE 0.5 ML', NULL, NULL, NULL),
        ('VACUNAS', '813', NULL, 'JERINGA SOCOREX 0,5ML JME SC 187.2.05005', NULL, NULL, NULL),
        ('VACUNAS', '831', NULL, 'NEVERA DE ICOPOR DE 10 LTS', NULL, NULL, NULL),
        ('VACUNAS', '850', NULL, 'AGUJA DES.HIPODERMICA CAL 20 G X 1""', NULL, NULL, NULL),
        ('VACUNAS', '862', NULL, 'TUBERIA POLIVINILO SOCOREX 1/4*1/8-5441', NULL, NULL, NULL),
        ('VACUNAS', '820', NULL, 'VIDRIO SOCOREX DE 1 ML', NULL, NULL, NULL),
        ('VACUNAS', '832', NULL, 'NEUTRHAL 3D X 1 LT', NULL, NULL, NULL),
        ('VACUNAS', '821', NULL, 'RESORTE  INYECTOR SOCOREX DE 0,5 ML', NULL, NULL, NULL),
        ('VACUNAS', '814', NULL, 'JERINGA DESECHABLE CON AGUJA X 20 ML', NULL, NULL, NULL),
        ('VACUNAS', '817', NULL, 'JERINGA DESECHABLE CON AGUJA X 50 ML', NULL, NULL, NULL),
        ('HUEVO', '528', NULL, 'HUEVO ROJO', NULL, 'UND', 'Primera'),
        ('HUEVO', '2121', NULL, 'HUEVO CRIOLLO', NULL, 'UND', 'Primera'),
        ('HUEVO', '5389', NULL, 'HUEVO CRIOLLO PRIMERAS POSTURAS SIN CLAS', NULL, 'UND', 'Primera'),
        ('HUEVO', '2776', NULL, 'HUEVO SIN CLAS BLANCO PRIMERAS POSTURAS', NULL, 'UND', 'Primera'),
        ('HUEVO', '2520', NULL, 'HUEVO BLANCO', NULL, 'UND', 'Primera'),
        ('HUEVO', '530', NULL, 'HUEVO GALLINA FELIZ', NULL, 'UND', 'Primera'),
        ('HUEVO', '2610', NULL, 'HUEVO SIN CLASIFICAR AZUR', NULL, 'UND', 'Primera'),
        ('HUEVO', '531', NULL, 'HUEVO BONEGG', NULL, 'UND', 'Primera'),
        ('HUEVO', '552', NULL, 'HUEVO LIBRE DE JAULA CERTIFICADO', NULL, 'UND', 'Primera'),
        ('HUEVO', '2756', NULL, 'HUEVO SIN CLAS ROJO PRIMERAS POSTURAS', NULL, 'UND', 'Primera'),
        ('HUEVO', '538', NULL, 'HUEVO MANCHADO ROJO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '537', NULL, 'HUEVO PICADO ROJO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2521', NULL, 'HUEVO FARFARA', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2125', NULL, 'HUEVO CRIOLLO PICADO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2523', NULL, 'HUEVO PICADO BLANCO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2522', NULL, 'HUEVO MANCHADO BLANCO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2124', NULL, 'HUEVO CRIOLLO MANCHADO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '1944', NULL, 'HUEVO RECUPERACION BOLSA KIL', NULL, 'KIL', 'Pnc'),
        ('HUEVO', '539', NULL, 'HUEVO DECOLORADO ROJO', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2697', NULL, 'HUEVO MANCHADO AZUR', NULL, 'UND', 'Pnc'),
        ('HUEVO', '2698', NULL, 'HUEVO PICADO AZUR', NULL, 'UND', 'Pnc')
    ) AS v(categoria, codigo, referencia, descripcion, tipo_inventario, um, tipo_huevo)
    WHERE NOT EXISTS (SELECT 1 FROM public.catalogo_items ci
                       WHERE ci.company_id = v_company_id AND ci.codigo = v.codigo);

    -- =================================================================================
    -- 12) INVENTARIO DE ALIMENTO — los 45 items ALIMENTO tambien al catalogo de
    --     inventario (item_inventario_ecuador, neutro EC/PA/CO). Se derivan del catalogo
    --     recien sembrado para no duplicar la lista. Estilo copiado de la empresa 1
    --     (tipo_item 'alimento', unidad 'kg', grupo NULL).
    -- =================================================================================
    INSERT INTO public.item_inventario_ecuador
           (codigo, nombre, tipo_item, unidad, activo, company_id, pais_id,
            created_at, updated_at, grupo,
            tipo_inventario_codigo, descripcion_tipo_inventario, referencia)
    SELECT ci.codigo, ci.nombre, 'alimento', 'kg', true, v_company_id, v_pais_id,
           now(), now(), NULL,
           'IN300511S', 'PRODUCTO TERMINADO GRAVADO SIN EXT', ci.metadata->>'referencia'
    FROM public.catalogo_items ci
    WHERE ci.company_id = v_company_id
      AND ci.item_type = 'alimento'
      AND NOT EXISTS (SELECT 1 FROM public.item_inventario_ecuador i
                       WHERE i.company_id = v_company_id AND i.codigo = ci.codigo);

    -- =================================================================================
    -- 13) LOTES — 10 lotes de La Esperanza. Sin aves ni galpon (no vienen en el Excel),
    --     sin ano de tabla genetica (la empresa todavia no cargo su guia).
    --     fecha_encaset anclada a las 12:00 UTC (regla de fechas puras).
    --     fase = replica EXACTA de LoteService.CreateAsync:
    --            semanas = (dias / 7) + 1  (0 si la fecha es futura/nula)  y  >= 26 -> 'Produccion'.
    -- =================================================================================
    INSERT INTO public.lotes
           (lote_nombre, granja_id, nucleo_id, galpon_id, fecha_encaset,
            hembras_l, machos_l, raza, ano_tabla_genetica, tipo_linea, fase,
            pais_id, pais_nombre, empresa_nombre,
            codigo_centro_costo, descripcion_centro_costo,
            company_id, created_by_user_id, created_at)
    SELECT v.lote_nombre, v_farm_id, NULL, NULL,
           (v.fecha_encaset + TIME '12:00') AT TIME ZONE 'UTC',
           NULL, NULL, v.raza, NULL, v.tipo_ave,
           CASE WHEN (CASE WHEN ((now() AT TIME ZONE 'utc')::date - v.fecha_encaset) < 0 THEN 0
                           ELSE GREATEST(0, (((now() AT TIME ZONE 'utc')::date - v.fecha_encaset) / 7) + 1)
                      END) >= 26
                THEN 'Produccion' ELSE 'Levante' END,
           v_pais_id, 'Colombia', c_empresa,
           v.codigo_centro_costo, v.descripcion_centro_costo,
           v_company_id, 1, now()
    FROM (VALUES
        ('LOTE 216', 'G3002216', 'LOTE 216 BABCOK BROWN', 'BABCOK BROWN', 'ROJA', DATE '2024-11-22'),
        ('LOTE 217', 'G3001217', 'LOTE 217 LOHMANN LSL', 'LOHMANN LSL', 'BLANCA', DATE '2024-11-14'),
        ('LOTE 218', 'G3002218', 'LOTE 218 BABCOK BROWN', 'BABCOK BROWN', 'ROJA', DATE '2025-01-24'),
        ('LOTE 221', 'G3001221', 'LOTE 221 LOHMANN LSL', 'LOHMANN LSL', 'BLANCA', DATE '2025-04-11'),
        ('LOTE 222', 'G3002222', 'LOTE 222 BABCOCK BROWN', 'BABCOK BROWN', 'ROJA', DATE '2025-05-09'),
        ('LOTE 223', 'G3002223', 'LOTE 223 BABCOCK BROWN', 'BABCOK BROWN', 'ROJA', DATE '2025-06-24'),
        ('LOTE 227', 'G3001227', 'LOTE 227 LOHMANN LSL', 'LOHMANN LSL', 'BLANCA', DATE '2025-08-26'),
        ('LOTE 229', 'G3001229', 'LOTE 229 LOHMANN BROWN', 'LOHMANN BROWN', 'ROJA', DATE '2025-11-07'),
        ('LOTE 231', 'G3001231', 'LOTE 231 LOHMANN LSL', 'LOHMANN LSL', 'BLANCA', DATE '2026-01-14'),
        ('LOTE 234', 'G3004234', 'LOTE 234 HY LINE', 'HY LINE', 'ROJA', DATE '2026-02-24')
    ) AS v(lote_nombre, codigo_centro_costo, descripcion_centro_costo, raza, tipo_ave, fecha_encaset)
    WHERE NOT EXISTS (SELECT 1 FROM public.lotes l
                       WHERE l.company_id = v_company_id
                         AND l.granja_id = v_farm_id
                         AND l.lote_nombre = v.lote_nombre
                         AND l.deleted_at IS NULL);

    -- 13a) Historial de etapa Levante (lo crea LoteService.CreateAsync junto al lote) ----
    INSERT INTO public.lote_etapa_levante
           (lote_id, aves_inicio_hembras, aves_inicio_machos, fecha_inicio, created_at)
    SELECT l.lote_id, COALESCE(l.hembras_l, 0), COALESCE(l.machos_l, 0),
           COALESCE(l.fecha_encaset, now()), now()
    FROM public.lotes l
    WHERE l.company_id = v_company_id AND l.granja_id = v_farm_id AND l.deleted_at IS NULL
      AND NOT EXISTS (SELECT 1 FROM public.lote_etapa_levante e WHERE e.lote_id = l.lote_id);

    -- 13b) Espejo LOTE POSTURA LEVANTE — INSERT solo si falta.
    --      En la BD local lo crea el trigger trg_lotes_sync_lote_postura_levante; en un
    --      entorno sin ese trigger lo crea este INSERT (mismos valores que el trigger).
    INSERT INTO public.lote_postura_levante
           (lote_nombre, granja_id, nucleo_id, galpon_id, fecha_encaset,
            hembras_l, machos_l, raza, ano_tabla_genetica, tipo_linea,
            pais_id, pais_nombre, empresa_nombre, lote_id,
            aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
            empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
            company_id, created_by_user_id, created_at,
            levante_traslado_ingreso_hembras, levante_traslado_ingreso_machos,
            levante_traslado_salida_hembras,  levante_traslado_salida_machos)
    SELECT l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.hembras_l, l.machos_l, l.raza, l.ano_tabla_genetica, l.tipo_linea,
           l.pais_id, l.pais_nombre, l.empresa_nombre, l.lote_id,
           l.hembras_l, l.machos_l, l.hembras_l, l.machos_l,
           l.company_id, l.created_by_user_id, l.fase, l.fase,
           GREATEST(0, ((now() AT TIME ZONE 'utc')::date - (l.fecha_encaset AT TIME ZONE 'utc')::date) / 7),
           CASE WHEN l.fase = 'Produccion' THEN 'Cerrado' ELSE 'Abierto' END,
           l.company_id, l.created_by_user_id, now(), 0, 0, 0, 0
    FROM public.lotes l
    WHERE l.company_id = v_company_id AND l.granja_id = v_farm_id AND l.deleted_at IS NULL
      AND NOT EXISTS (SELECT 1 FROM public.lote_postura_levante x WHERE x.lote_id = l.lote_id);

    -- 13c) Espejo LEVANTE — alinear a los valores deseados. La guarda IS DISTINCT FROM
    --      deja el UPDATE en 0 filas en la 2a pasada (el trigger de historico dispara por
    --      fila actualizada: sin guarda ensuciaria historico_lote_postura).
    UPDATE public.lote_postura_levante lpl
       SET raza                             = l.raza,
           fecha_encaset                    = l.fecha_encaset,
           tipo_linea                       = l.tipo_linea,
           estado                           = l.fase,
           etapa                            = l.fase,
           estado_cierre                    = CASE WHEN l.fase = 'Produccion' THEN 'Cerrado' ELSE 'Abierto' END,
           company_id                       = l.company_id,
           empresa_id                       = l.company_id,
           pais_id                          = l.pais_id,
           pais_nombre                      = l.pais_nombre,
           empresa_nombre                   = l.empresa_nombre,
           levante_traslado_ingreso_hembras = 0,
           levante_traslado_ingreso_machos  = 0,
           levante_traslado_salida_hembras  = 0,
           levante_traslado_salida_machos   = 0
      FROM public.lotes l
     WHERE lpl.lote_id = l.lote_id
       AND l.company_id = v_company_id AND l.granja_id = v_farm_id AND l.deleted_at IS NULL
       AND (lpl.raza           IS DISTINCT FROM l.raza
         OR lpl.fecha_encaset  IS DISTINCT FROM l.fecha_encaset
         OR lpl.tipo_linea     IS DISTINCT FROM l.tipo_linea
         OR lpl.estado         IS DISTINCT FROM l.fase
         OR lpl.etapa          IS DISTINCT FROM l.fase
         OR lpl.estado_cierre  IS DISTINCT FROM (CASE WHEN l.fase = 'Produccion' THEN 'Cerrado' ELSE 'Abierto' END)
         OR lpl.company_id     IS DISTINCT FROM l.company_id
         OR lpl.empresa_id     IS DISTINCT FROM l.company_id
         OR lpl.pais_id        IS DISTINCT FROM l.pais_id
         OR lpl.pais_nombre    IS DISTINCT FROM l.pais_nombre
         OR lpl.empresa_nombre IS DISTINCT FROM l.empresa_nombre
         OR lpl.levante_traslado_ingreso_hembras <> 0
         OR lpl.levante_traslado_ingreso_machos  <> 0
         OR lpl.levante_traslado_salida_hembras  <> 0
         OR lpl.levante_traslado_salida_machos   <> 0);

    -- 13d) Espejo LOTE POSTURA PRODUCCION — solo para los lotes en fase 'Produccion'.
    --      Semantica copiada de LotePosturaLevanteService.CerrarLoteYCrearProduccionAsync:
    --      nombre 'P-<lote>', estado/etapa 'Produccion', estado_cierre 'Abierta',
    --      aves = las disponibles del levante (aqui 0: los lotes vienen sin aves).
    INSERT INTO public.lote_postura_produccion
           (lote_nombre, granja_id, nucleo_id, galpon_id, fecha_encaset,
            hembras_l, machos_l, raza, ano_tabla_genetica, tipo_linea,
            pais_id, pais_nombre, empresa_nombre,
            fecha_inicio_produccion, hembras_iniciales_prod, machos_iniciales_prod, huevos_iniciales,
            lote_id, lote_postura_levante_id,
            aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
            empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
            company_id, created_by_user_id, created_at,
            produccion_traslado_ingreso_hembras, produccion_traslado_ingreso_machos,
            produccion_traslado_salida_hembras,  produccion_traslado_salida_machos)
    SELECT 'P-' || l.lote_nombre, lpl.granja_id, lpl.nucleo_id, lpl.galpon_id, lpl.fecha_encaset,
           lpl.hembras_l, lpl.machos_l, lpl.raza, lpl.ano_tabla_genetica, lpl.tipo_linea,
           lpl.pais_id, lpl.pais_nombre, lpl.empresa_nombre,
           now(),
           GREATEST(0, COALESCE(lpl.aves_h_actual, 0)),
           GREATEST(0, COALESCE(lpl.aves_m_actual, 0)),
           0,
           lpl.lote_id, lpl.lote_postura_levante_id,
           GREATEST(0, COALESCE(lpl.aves_h_actual, 0)),
           GREATEST(0, COALESCE(lpl.aves_m_actual, 0)),
           GREATEST(0, COALESCE(lpl.aves_h_actual, 0)),
           GREATEST(0, COALESCE(lpl.aves_m_actual, 0)),
           lpl.company_id, lpl.created_by_user_id, 'Produccion', 'Produccion', lpl.edad, 'Abierta',
           lpl.company_id, lpl.created_by_user_id, now(), 0, 0, 0, 0
    FROM public.lotes l
    JOIN public.lote_postura_levante lpl
      ON lpl.lote_id = l.lote_id AND lpl.deleted_at IS NULL
    WHERE l.company_id = v_company_id AND l.granja_id = v_farm_id AND l.deleted_at IS NULL
      AND l.fase = 'Produccion'
      AND NOT EXISTS (SELECT 1 FROM public.lote_postura_produccion p
                       WHERE p.lote_postura_levante_id = lpl.lote_postura_levante_id
                         AND p.deleted_at IS NULL);
END
$sr$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simetrico y best-effort: borra SOLO lo de Santa Reyes, en orden inverso al Up().
            // Todo va acotado por company_id / nombre de rol / email, de modo que ninguna otra
            // empresa se ve afectada. Si la empresa no existe, no hace nada.
            migrationBuilder.Sql(@"
DO $sr$
DECLARE
    v_company_id integer;
    v_farm_ids   integer[];
    v_user_ids   uuid[];
    v_role_ids   integer[];
BEGIN
    SELECT c.id INTO v_company_id
    FROM public.companies c WHERE c.name = 'Santa Reyes' ORDER BY c.id LIMIT 1;

    IF v_company_id IS NULL THEN
        RETURN;
    END IF;

    SELECT COALESCE(array_agg(f.id), '{}'::integer[]) INTO v_farm_ids
    FROM public.farms f WHERE f.company_id = v_company_id;

    SELECT COALESCE(array_agg(DISTINCT ul.user_id), '{}'::uuid[]) INTO v_user_ids
    FROM public.user_logins ul
    JOIN public.logins l ON l.id = ul.login_id
    WHERE lower(l.email) IN ('admin@santareyes.com', 'implementador@santareyes.com');

    SELECT COALESCE(array_agg(r.id), '{}'::integer[]) INTO v_role_ids
    FROM public.roles r
    WHERE r.name IN ('Santa Reyes Administrador', 'Santa Reyes Implementador');

    -- 13) lotes y espejos (produccion -> levante -> etapa -> lotes; historico cascadea)
    DELETE FROM public.lote_postura_produccion WHERE company_id = v_company_id;
    DELETE FROM public.lote_postura_levante    WHERE company_id = v_company_id;
    DELETE FROM public.lote_etapa_levante e
     USING public.lotes l
     WHERE l.lote_id = e.lote_id AND l.company_id = v_company_id;
    DELETE FROM public.lotes WHERE company_id = v_company_id;
    DELETE FROM public.historico_lote_postura WHERE company_id = v_company_id;

    -- 12/11) inventario y catalogo
    DELETE FROM public.item_inventario_ecuador WHERE company_id = v_company_id;
    DELETE FROM public.catalogo_items          WHERE company_id = v_company_id;

    -- 10/9/8/7/6) accesos a granja y estructura fisica
    DELETE FROM public.user_farms WHERE farm_id = ANY(v_farm_ids);
    DELETE FROM public.farm_silos WHERE company_id = v_company_id;
    DELETE FROM public.galpones   WHERE company_id = v_company_id;
    DELETE FROM public.nucleos    WHERE company_id = v_company_id;
    DELETE FROM public.farms      WHERE company_id = v_company_id;

    -- 5) usuarios
    DELETE FROM public.user_roles     WHERE user_id = ANY(v_user_ids);
    DELETE FROM public.user_companies WHERE user_id = ANY(v_user_ids);
    DELETE FROM public.user_logins    WHERE user_id = ANY(v_user_ids);
    DELETE FROM public.logins WHERE lower(email) IN ('admin@santareyes.com', 'implementador@santareyes.com');
    DELETE FROM public.users  WHERE id = ANY(v_user_ids);

    -- 4) menus de empresa y de rol
    DELETE FROM public.role_menus    WHERE role_id = ANY(v_role_ids);
    DELETE FROM public.company_menus WHERE company_id = v_company_id;

    -- 3) roles
    DELETE FROM public.role_permissions WHERE role_id = ANY(v_role_ids);
    DELETE FROM public.role_companies   WHERE role_id = ANY(v_role_ids);
    DELETE FROM public.roles            WHERE id = ANY(v_role_ids);

    -- 2) lista maestra regional
    DELETE FROM public.master_list_options o
     USING public.master_lists ml
     WHERE ml.id = o.master_list_id AND ml.company_id = v_company_id;
    DELETE FROM public.master_lists WHERE company_id = v_company_id;

    -- 1) empresa
    DELETE FROM public.company_pais WHERE company_id = v_company_id;
    DELETE FROM public.companies    WHERE id = v_company_id;
END
$sr$;
");
        }
    }
}
