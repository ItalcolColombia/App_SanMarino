// file: src/ZooSanMarino.Application/DTOs/Shared/LiteDtos.cs
namespace ZooSanMarino.Application.DTOs.Shared;


public sealed record NucleoLiteDto(string NucleoId, string NucleoNombre, int GranjaId);
public sealed record GalponLiteDto(string GalponId, string GalponNombre, string NucleoId, int GranjaId);
