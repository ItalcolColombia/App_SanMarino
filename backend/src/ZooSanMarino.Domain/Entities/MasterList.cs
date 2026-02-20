// src/ZooSanMarino.Domain/Entities/MasterList.cs
namespace ZooSanMarino.Domain.Entities;

public class MasterList
{
    public int    Id    { get; set; }
    public string Key   { get; set; } = null!;
    public string Name  { get; set; } = null!;

    // Nuevos campos para vincular con compañía y país
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? CountryId { get; set; }  // PaisId
    public string? CountryName { get; set; }  // PaisNombre

    public ICollection<MasterListOption> Options { get; set; } = new List<MasterListOption>();
}
