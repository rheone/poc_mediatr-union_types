using System.Security.Claims;
using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Update;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="UpdateProductValidator"/>'s rules directly, including the id check that <see cref="MediatrUnionPoc.Application.Features.Products.Create.CreateProductValidator"/> doesn't need.</summary>
public class UpdateProductValidatorTests
{
    private readonly UpdateProductValidator _sut = new();
    private static readonly ClaimsPrincipal AnonymousPrincipal = new(new ClaimsIdentity());

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Empty_id_fails_validation()
    {
        var result = _sut.TestValidate(
            new UpdateProductCommand(Guid.Empty, "Widget", 10m, AnonymousPrincipal)
        );

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    /// <summary>Verifies an empty or whitespace-only name fails validation.</summary>
    /// <param name="name">An empty or whitespace-only name value that should fail validation.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_name_fails_validation(string name)
    {
        var result = _sut.TestValidate(
            new UpdateProductCommand(Guid.NewGuid(), name, 10m, AnonymousPrincipal)
        );

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a negative price fails validation.</summary>
    /// <param name="price">A negative price value that should fail validation.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public void Negative_price_fails_validation(decimal price)
    {
        var result = _sut.TestValidate(
            new UpdateProductCommand(Guid.NewGuid(), "Widget", price, AnonymousPrincipal)
        );

        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    /// <summary>Verifies a fully valid command produces no validation errors.</summary>
    [Fact]
    public void Valid_commands_produce_no_errors()
    {
        var result = _sut.TestValidate(
            new UpdateProductCommand(Guid.NewGuid(), "Widget", 10m, AnonymousPrincipal)
        );

        result.ShouldNotHaveAnyValidationErrors();
    }
}
