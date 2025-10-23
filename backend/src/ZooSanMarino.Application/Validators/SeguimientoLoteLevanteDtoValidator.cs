/// file: backend/src/ZooSanMarino.Application/Validators/SeguimientoLoteLevanteDtoValidator.cs
using FluentValidation;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Validators;

public class SeguimientoLoteLevanteDtoValidator : AbstractValidator<SeguimientoLoteLevanteDto>
{
    public SeguimientoLoteLevanteDtoValidator()
    {
        RuleFor(x => x.LoteId).GreaterThan(0);
        RuleFor(x => x.FechaRegistro).LessThanOrEqualTo(DateTime.UtcNow.AddDays(1)); // tolerancia TZ
        RuleFor(x => x.MortalidadHembras).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MortalidadMachos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SelH).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SelM).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ErrorSexajeHembras).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ErrorSexajeMachos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConsumoKgHembras).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TipoAlimento).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Ciclo).NotEmpty().Must(c => new[] { "Normal" }.Contains(c))
            .WithMessage("Ciclo inválido (permitido: Normal).");
        RuleFor(x => x.Observaciones).MaximumLength(1000).When(x => x.Observaciones is not null);
    }
}
