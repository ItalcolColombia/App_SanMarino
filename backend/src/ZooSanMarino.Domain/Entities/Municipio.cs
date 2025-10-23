// ZooSanMarino.Domain/Entities/Municipio.cs
namespace ZooSanMarino.Domain.Entities;
public class Municipio
{
    public int    MunicipioId       { get; set; }      // PK simple
    public string MunicipioNombre   { get; set; } = null!;
    public int    DepartamentoId    { get; set; }
    public Departamento Departamento { get; set; } = null!;
}
