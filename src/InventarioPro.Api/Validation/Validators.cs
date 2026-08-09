using FluentValidation;
using InventarioPro.Api.Dtos;

namespace InventarioPro.Api.Validation;

/// <summary>
/// Validación de entrada centralizada. Todo lo que llega del cliente pasa por acá
/// antes de tocar la base: longitudes, rangos y formatos.
/// </summary>
public class ProductCreateValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("El SKU es obligatorio.")
            .MaximumLength(40)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("El SKU solo admite letras, números, punto, guion y guion bajo.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.")
            .LessThan(1_000_000_000);

        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.")
            .LessThan(1_000_000_000);

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock mínimo no puede ser negativo.");

        RuleFor(x => x.InitialStock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock inicial no puede ser negativo.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Hay que indicar una categoría.");

        RuleFor(x => x.SupplierId)
            .GreaterThan(0).When(x => x.SupplierId.HasValue)
            .WithMessage("El proveedor indicado no es válido.");
    }
}

public class ProductUpdateValidator : AbstractValidator<ProductUpdateDto>
{
    public ProductUpdateValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().MaximumLength(40)
            .Matches("^[A-Za-z0-9._-]+$");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).LessThan(1_000_000_000);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).LessThan(1_000_000_000);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0).When(x => x.SupplierId.HasValue);
    }
}

public class CategoryCreateValidator : AbstractValidator<CategoryCreateDto>
{
    public CategoryCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}

public class SupplierCreateValidator : AbstractValidator<SupplierCreateDto>
{
    public SupplierCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public class StockMovementCreateValidator : AbstractValidator<StockMovementCreateDto>
{
    public StockMovementCreateValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);

        RuleFor(x => x.Type).IsInEnum()
            .WithMessage("El tipo de movimiento debe ser In, Out o Adjustment.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("La cantidad no puede ser negativa.")
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Reason).MaximumLength(300);
        RuleFor(x => x.Reference).MaximumLength(60);
    }
}

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(120);

        RuleFor(x => x.FullName)
            .NotEmpty().MaximumLength(120);

        // Coincide con la política configurada en Identity (ver Program.cs).
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe incluir al menos un símbolo.");
    }
}

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class RefreshValidator : AbstractValidator<RefreshDto>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
