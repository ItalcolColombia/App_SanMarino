// src/ZooSanMarino.Domain/Entities/ProduccionAvicolaRaw.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionAvicolaRaw : AuditableEntity
{
    public int Id { get; set; }
    public string? AnioGuia { get; set; }
    public string? Raza { get; set; }
    public string? Edad { get; set; }
    public string? MortSemH { get; set; }
    public string? RetiroAcH { get; set; }
    public string? MortSemM { get; set; }
    public string? RetiroAcM { get; set; }
    public string? ConsAcH { get; set; }
    public string? ConsAcM { get; set; }
    public string? GrAveDiaH { get; set; }
    public string? GrAveDiaM { get; set; }
    public string? PesoH { get; set; }
    public string? PesoM { get; set; }
    public string? Uniformidad { get; set; }
    public string? HTotalAa { get; set; }
    public string? ProdPorcentaje { get; set; }
    public string? HIncAa { get; set; }
    public string? AprovSem { get; set; }
    public string? PesoHuevo { get; set; }
    public string? MasaHuevo { get; set; }
    public string? GrasaPorcentaje { get; set; }
    public string? NacimPorcentaje { get; set; }
    public string? PollitoAa { get; set; }
    public string? KcalAveDiaH { get; set; }
    public string? KcalAveDiaM { get; set; }
    public string? AprovAc { get; set; }
    public string? GrHuevoT { get; set; }
    public string? GrHuevoInc { get; set; }
    public string? GrPollito { get; set; }
    public string? Valor1000 { get; set; }
    public string? Valor150 { get; set; }
    public string? Apareo { get; set; }
    public string? PesoMh { get; set; }
}
