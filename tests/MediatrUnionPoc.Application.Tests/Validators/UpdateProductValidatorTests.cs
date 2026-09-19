using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Update;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="UpdateProductValidator"/>'s rules directly, including the id check that <see cref="MediatrUnionPoc.Application.Features.Products.Create.CreateProductValidator"/> doesn't need.</summary>
public class UpdateProductValidatorTests
{
    private readonly UpdateProductValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Id_must_not_be_empty()
    {
        var result = _sut.TestValidate(new UpdateProductCommand(Guid.Empty, "Widget", 10m));

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    /// <summary>Verifies an empty or whitespace-only name fails validation.</summary>
    /// <param name="name">An empty or whitespace-only name value that should fail validation.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_must_not_be_empty_or_whitespace(string name)
    {
        var result = _sut.TestValidate(new UpdateProductCommand(Guid.NewGuid(), name, 10m));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a negative price fails validation.</summary>
    /// <param name="price">A negative price value that should fail validation.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public void Price_must_not_be_negative(decimal price)
    {
        var result = _sut.TestValidate(new UpdateProductCommand(Guid.NewGuid(), "Widget", price));

        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    /// <summary>Verifies a fully valid command produces no validation errors.</summary>
    [Fact]
    public void Valid_commands_produce_no_errors()
    {
        var result = _sut.TestValidate(new UpdateProductCommand(Guid.NewGuid(), "Widget", 10m));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
