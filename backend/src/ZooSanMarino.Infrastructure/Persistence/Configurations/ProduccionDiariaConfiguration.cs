using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración para ignorar ProduccionDiaria del modelo de EF Core.
/// Esta entidad está duplicada con SeguimientoProduccion que mapea a la misma tabla.
/// Usamos SeguimientoProduccion en su lugar.
/// </summary>
public class ProduccionDiariaConfiguration : IEntityTypeConfiguration<ProduccionDiaria>
{
    public void Configure(EntityTypeBuilder<ProduccionDiaria> builder)
    {
        // Ignorar esta entidad completamente del modelo
        // porque SeguimientoProduccion ya mapea a la tabla produccion_diaria
        builder.ToTable("_ignored_produccion_diaria", t => t.ExcludeFromMigrations());
        
        // Alternativamente, puedes comentar la línea anterior y descomentar la siguiente:
        // builder.Ignore("ProduccionDiaria"); // Esto también funciona
    }
}



