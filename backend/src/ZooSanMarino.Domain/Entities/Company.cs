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
        /// LEVANTE a partir de la semana <c>HuevosLevanteCalculos.SemanaMinimaHuevosLevante</c> (14),
        /// con la misma clasificadora fija de producción. Al liquidar el levante, el acumulado se
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

        // ← Añadimos las colecciones de navegación:
        public ICollection<Farm> Farms { get; set; } = new List<Farm>();
        public ICollection<Regional> Regionales { get; set; } = new List<Regional>();
        public ICollection<Zona> Zonas { get; set; } = new List<Zona>();
        public ICollection<RoleCompany> RoleCompanies { get; set; } = new List<RoleCompany>();
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
        public ICollection<CompanyPais> CompanyPaises { get; set; } = new List<CompanyPais>();
        public ICollection<CompanyMenu> CompanyMenus { get; set; } = new List<CompanyMenu>();
        public CompanyLogo? Logo { get; set; }
    }
}
