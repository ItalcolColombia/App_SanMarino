namespace ZooSanMarino.Domain.Entities
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Identifier { get; set; } = null!;  // antes Nit
        public string DocumentType { get; set; } = null!;

        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string[] VisualPermissions { get; set; } = Array.Empty<string>();
        public bool MobileAccess { get; set; }

        /// <summary>
        /// Default global de la empresa: <c>true</c> = el alimento se maneja a nivel GALPÓN
        /// (exige núcleo/galpón, como Ecuador/Panamá); <c>false</c> = a nivel GRANJA (Colombia).
        /// Cada granja puede overridear vía <see cref="Farm.ManejaAlimentoPorGalpon"/> (nullable).
        /// </summary>
        public bool ManejaAlimentoPorGalpon { get; set; }

        /// <summary>
        /// <c>true</c> = la empresa maneja códigos ERP avícolas (bodega, centro de operación,
        /// instalación, ubicación y centro de costo) en granja/núcleo/galpón/lote; el front muestra
        /// esos campos solo cuando el flag está activo. <c>false</c> (default) = comportamiento
        /// actual, sin campos ERP.
        /// </summary>
        public bool ManejaCodigosErpAvicola { get; set; }

        /// <summary>
        /// <c>true</c> = la empresa clasifica los huevos del seguimiento diario de producción POR
        /// ÍTEMS del catálogo de inventario (Primera/Pnc), no por las 11 columnas fijas
        /// (<c>huevo_limpio</c>…<c>huevo_otro</c>). Con el flag activo el desglose viaja en
        /// <c>seguimiento_diario_produccion.metadata → huevoItems</c>, <c>huevo_tot</c> guarda la suma
        /// (mantiene vivos espejo/trigger/indicadores) y las 11 columnas + <c>huevo_inc</c> quedan en 0.
        /// <c>false</c> (default) = comportamiento actual, clasificación por columnas fijas.
        /// </summary>
        public bool ClasificacionHuevoPorItems { get; set; }

        /// <summary>
        /// <c>true</c> = la empresa captura la CLASIFICACIÓN DE HUEVOS en el seguimiento diario de
        /// LEVANTE (tab fijo, sin gate de semana desde jul-2026; solo se rechaza una fecha anterior
        /// al encaset), con la misma clasificadora fija de producción. Al liquidar el levante, el acumulado se
        /// arrastra al primer registro de producción (la fecha de inicio de producción) y, si ese día
        /// el usuario registra producción, los huevos se SUMAN sobre esa fila.
        /// <c>false</c> (default) = comportamiento actual: levante no captura huevos y la liquidación
        /// no arrastra nada.
        /// </summary>
        public bool CapturaHuevosEnLevante { get; set; }

        /// <summary>
        /// <c>true</c> = la empresa puede trasladar aves ENTRE ETAPAS (Levante → Producción) desde el
        /// seguimiento diario, liquidando el lote de levante contra un lote de producción que ya tiene
        /// aves de otra edad. El sentido inverso (Producción → Levante) NUNCA se permite.
        /// <c>false</c> (default) = comportamiento actual, solo traslados dentro de la misma etapa.
        /// Las aves recibidas conservan su edad vía <see cref="LoteAvesCohorte"/>.
        /// </summary>
        public bool PermiteTrasladoAvesCrossEtapa { get; set; }

        /// <summary>
        /// <c>true</c> = en las VENTAS de pollo engorde de la empresa, el peso báscula (bruto/tara)
        /// NO es obligatorio al registrar la venta: la báscula llega al día siguiente (Panamá). La
        /// venta nace sin peso en estado <c>Pendiente</c> y el peso se carga al CONFIRMARLA, momento
        /// en que se re-prorratea por lote (neto, promedio por ave y pesos reales) en la misma
        /// transacción que la pasa a <c>Completado</c>. Así nunca existe una venta <c>Completado</c>
        /// sin peso y la liquidación queda intacta.
        /// <c>false</c> (default) = comportamiento actual: sin peso no se registra la venta.
        /// </summary>
        public bool VentaEngordePesoDiferido { get; set; }

        /// <summary>
        /// <c>true</c> = la empresa NO maneja el pollo engorde por sexo una vez que el lote sale de
        /// reproductora (Panamá): mortalidad, selección y consumo se digitan como un único total
        /// MIXTO. Solo cambia la PRESENTACIÓN de la carga masiva — la plantilla descargable emite
        /// columnas «Mixta/Mixto» en lugar del par H/M — mientras el dato sigue guardándose en los
        /// mismos campos (el sistema suma H+M en todos sus cálculos).
        /// <c>false</c> (default) = plantilla con columnas por sexo, comportamiento actual.
        /// <para>
        /// OJO: este flag NO decide de qué bucket se descuentan las aves. Eso se resuelve por DATOS
        /// del lote (ver <c>RetiroAvesEngordeCalculos.EsLoteMixto</c>), de modo que un lote mixto
        /// descuenta de <c>mixtas</c> en cualquier empresa.
        /// </para>
        /// </summary>
        public bool SeguimientoEngordeMixto { get; set; }

        /// <summary>
        /// Días ANTES del encasetamiento cuyo alimento ya cuenta como del lote en el saldo del reporte
        /// diario de engorde. Default 10.
        /// <para>
        /// El saldo descarta lo anterior al lote para no heredar el sobrante del ciclo previo del
        /// galpón (FIX #12), pero en engorde el preiniciador llega ANTES que los pollitos: cortar en
        /// la fecha de encaset dejaba fuera alimento propio — en el galpón 6 de DAYLAND, 12.129,638 kg
        /// recibidos 4 días antes, justo lo que separaba el reporte del inventario real.
        /// </para>
        /// <para>
        /// Es un parámetro operativo, no un flag de comportamiento: cada empresa lo ajusta a su vacío
        /// sanitario. El default (10) queda por debajo del típico (10-14 días), así que la ventana no
        /// puede alcanzar el cierre del lote anterior.
        /// </para>
        /// </summary>
        public int DiasAlimentoPrevioEncaset { get; set; } = 10;

        /// <summary>
        /// <c>true</c> = en el Reporte Diario de Costos de engorde, el stock y el consumo por tipo de
        /// alimento se toman de las fuentes REALES (ingresos del histórico y consumo del seguimiento
        /// diario) en vez del snapshot jsonb <c>historico_consumo_alimento</c>.
        /// <para>
        /// Ese snapshot está incompleto: suma 1.554.181,4 kg contra los 1.706.089,8 kg de consumo real,
        /// y su <c>saldo_final</c> solo existe para los alimentos consumidos ESE día, así que el stock
        /// mostrado era una fracción del real (G0464 al 22/07: 46.229,2 kg de un galpón que tenía
        /// 66.565,8 en tres ítems) y no se movía al recalcularse el saldo.
        /// </para>
        /// <para>
        /// Es un flag y no un cambio global porque el desglose depende de la calidad de
        /// <c>tipo_alimento</c>: en Panamá es el nombre limpio del ítem, pero en Ecuador viene con
        /// prefijo de sexo (<c>"H: AV. SUPER POLLO ENGORDE"</c>) y no cruza con el inventario. Con el
        /// flag en OFF el reporte se comporta exactamente como antes.
        /// </para>
        /// </summary>
        public bool ReporteCostosAlimentoDesdeFuentesReales { get; set; }

        /// <summary>
        /// <c>true</c> = en esta empresa la HORA de llegada de las aves decide el primer día con
        /// registro del lote (pollo engorde y reproductora): desde las 13:00 las aves ya no alcanzan a
        /// consumir ese día y el primer consumo pasa al día siguiente del encasetamiento.
        /// <c>false</c> (default) = comportamiento previo: el primer registro puede ir en el día del
        /// encasetamiento y la hora informada se ignora.
        /// <para>
        /// La fecha de encasetamiento y la edad NO cambian en ningún caso: la edad se sigue contando
        /// desde <c>fecha_encaset</c>, así que un lote tardío arranca en edad 1.
        /// </para>
        /// </summary>
        public bool PrimerRegistroSegunHoraLlegada { get; set; }

        /// <summary>
        /// <c>true</c> = en esta empresa los lotes de pollo engorde se PROGRAMAN: el catálogo de
        /// <c>lote_base_engorde</c> (asignado por granja) es la lista de lotes que se van a encasetar, el
        /// nombre del lote sale obligatoriamente de esa lista (numerado por corrida dentro del galpón) y
        /// un gasto de inventario puede cargarse contra un lote PROGRAMADO que todavía no existe
        /// (desinsectación previa al encaset), re-atribuyéndose solo al crearse el lote real.
        /// <c>false</c> (default) = comportamiento previo: nombre libre, lote base opcional, sin
        /// pestaña de programación y gasto solo contra lotes reales.
        /// </summary>
        public bool ProgramacionLotesEngorde { get; set; }

        /// <summary>
        /// Cómo nombra la empresa el lote que abre desde una programación (sólo aplica con
        /// <see cref="ProgramacionLotesEngorde"/> activo).
        /// <para>
        /// <c>true</c> (Panamá): nombre = "{lote base} - {corrida}" desde la primera — "96 - 1".
        /// </para>
        /// <para>
        /// <c>false</c> (default, Ecuador): el nombre del lote ES el del lote base — "2603" —, porque
        /// la corrida ya está codificada en el nombre del base (año + número) y hay un lote por galpón
        /// en cada corrida. El sufijo sólo aparece desde la segunda apertura del mismo base en el mismo
        /// galpón, para no repetir nombre dentro del galpón.
        /// </para>
        /// </summary>
        public bool NombreLoteIncluyeCorrida { get; set; }

        /// <summary>
        /// <c>true</c> = el inventario de esta empresa se ubica en SILOS y BODEGAS
        /// (<see cref="FarmSilo"/>), no en el galpón. Ingresos, traslados y consumos exigen
        /// <c>silo_id</c> y persisten <c>nucleo_id</c>/<c>galpon_id</c> en NULL: el galpón pasa a ser
        /// el filtro que despliega qué silos se pueden elegir, no la ubicación del stock.
        /// <para>
        /// Nace del dato del cliente (Santa Reyes): en su ERP el galpón mueve aves, huevo e insumos,
        /// pero el ALIMENTO se mueve en el silo, y los silos cuelgan de la bodega de la GRANJA. Como
        /// un mismo silo puede alimentar a varios galpones, llevar el saldo por (galpón, silo)
        /// partiría el contenido físico de un silo en dos saldos y ninguno sería el real.
        /// </para>
        /// <para>
        /// <c>false</c> (default) = comportamiento previo intacto: la ubicación es granja (Colombia) o
        /// granja/núcleo/galpón (Ecuador, Panamá) y <c>silo_id</c> es siempre NULL.
        /// </para>
        /// </summary>
        public bool ManejaInventarioPorSilo { get; set; }

        /// <summary>
        /// Los reportes <b>Contable</b> y <b>Técnico</b> leen el ALIMENTO del módulo de inventario
        /// unificado (<c>inventario_gestion_movimiento</c>) en vez de la tabla vieja
        /// (<c>farm_inventory_movements</c>).
        ///
        /// <para>
        /// Una empresa que ya opera sobre el módulo nuevo no tiene ni una fila en la tabla vieja, así
        /// que sus columnas de alimento (entradas, traslados, retiros, saldo de bultos) salen en CERO
        /// sin ningún error a la vista. Este flag es lo que las hace leer donde su alimento realmente
        /// está.
        /// </para>
        ///
        /// <para>
        /// <c>false</c> (default) = la consulta de siempre, byte a byte. Se enciende empresa por
        /// empresa y verificando: cambiar la fuente de un reporte contable mueve números que alguien
        /// ya concilió.
        /// </para>
        /// </summary>
        public bool ReportesAlimentoDesdeInventarioUnificado { get; set; }

        /// <summary>
        /// Los seguimientos diarios de esta empresa exigen una <b>doble validación</b>: al guardar, el
        /// registro queda pendiente y el alimento y las aves consumidas se <b>separan</b> (reservan) en
        /// vez de descontarse. El descuento real ocurre recién cuando alguien con permiso valida el
        /// registro.
        ///
        /// <para>
        /// Nace de un problema concreto de operación: el registro se puede editar y borrar después de
        /// guardado, así que cada corrección obligaba a devolver inventario y recalcular saldos a mano.
        /// Separando, editar es reescribir la reserva — no hay nada que devolver porque nunca se
        /// descontó—. Y como el mismo galpón alimenta a dos lotes, la reserva es además lo que permite
        /// distinguir qué se llevó cada uno: el disponible pasa a ser <c>stock − reservas activas</c>.
        /// </para>
        ///
        /// <para>
        /// Trae aparejado el resto del control: un registro sin validar pasado el día queda «en
        /// retraso» (fila roja, alarma y modal al entrar al lote) y bloquea el alta de días nuevos en
        /// ese lote.
        /// </para>
        ///
        /// <para>
        /// <c>false</c> (default) = comportamiento previo intacto: se descuenta al guardar, no se
        /// separa nada y ningún registro se pinta ni bloquea. Se enciende empresa por empresa —cambiar
        /// el momento del descuento mueve inventario en producción—.
        /// </para>
        /// </summary>
        public bool RequiereValidacionSeguimientoDiario { get; set; }

        // ← Añadimos las colecciones de navegación:
        public ICollection<Farm> Farms { get; set; } = new List<Farm>();
        public ICollection<Regional> Regionales { get; set; } = new List<Regional>();
        public ICollection<Zona> Zonas { get; set; } = new List<Zona>();
        public ICollection<RoleCompany> RoleCompanies { get; set; } = new List<RoleCompany>();
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
        public ICollection<CompanyPais> CompanyPaises { get; set; } = new List<CompanyPais>();
        public ICollection<CompanyMenu> CompanyMenus { get; set; } = new List<CompanyMenu>();
        public ICollection<CompanyPermission> CompanyPermissions { get; set; } = new List<CompanyPermission>();
        public CompanyLogo? Logo { get; set; }
    }
}
