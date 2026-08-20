namespace ZooSanMarino.Domain.Entities
{
    /// <summary>
    /// Guia genetica de postura, dedicada a empresas que necesitan un modelo simple (una linea =
    /// raza/hibrido, una curva de produccion/mortalidad/consumo por semana), separada a proposito de
    /// <see cref="ProduccionAvicolaRaw"/> (tabla <c>guia_genetica_sanmarino_colombia</c>): esa es
    /// compartida entre reproductora, engorde y postura de Sanmarino/Panama/Ecuador, con ~50 columnas
    /// para casos que esta empresa no tiene. Nace con Santa Reyes (5 lineas: Babcock Brown, Hy Line
    /// Brown, Lohmann LSL, Criolla, Azur) pero no esta atada a esa empresa por nombre.
    /// </summary>
    public class GuiaGeneticaSantaReyes : AuditableEntity
    {
        public int Id { get; set; }

        /// <summary>Nombre de la linea genetica (p.ej. "Babcock Brown"). Es el valor que ve el usuario
        /// en el selector de raza al crear el lote: no hay traduccion a un codigo corto.</summary>
        public string Raza { get; set; } = null!;

        /// <summary>Anio de la guia (p.ej. "2026"), permite versionar sin pisar una guia anterior.</summary>
        public string AnioGuia { get; set; } = null!;

        /// <summary>Semana de vida del lote (18 en adelante: la guia arranca en produccion).</summary>
        public int Edad { get; set; }

        /// <summary>% de produccion (postura) de la semana. Null = la linea no tiene dato para esa
        /// semana (p.ej. Criolla no trae produccion desde la semana 101: se apaga antes que las demas).</summary>
        public decimal? ProdPorcentaje { get; set; }

        /// <summary>% de mortalidad ACUMULADA de hembras a esa semana (no semanal).</summary>
        public decimal? RetiroAcH { get; set; }

        /// <summary>Consumo en gramos/ave/dia de hembras a esa semana.</summary>
        public decimal? GrAveDiaH { get; set; }

        /// <summary>Clave natural derivada, Raza+AnioGuia+Edad, para upsert idempotente y lookup
        /// directo. Se recalcula si Raza/AnioGuia/Edad cambian.</summary>
        public string? CodigoGuiaGenetica { get; set; }
    }
}
