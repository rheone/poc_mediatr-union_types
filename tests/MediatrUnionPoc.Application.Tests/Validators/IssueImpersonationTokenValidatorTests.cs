using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using MediatrUnionPoc.Application.Tests.TestData;
using static MediatrUnionPoc.Application.Tests.TestData.ImpersonationMother;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises every rule of <see cref="IssueImpersonationTokenValidator"/>, which the pipeline tests substitute away.</summary>
public class IssueImpersonationTokenValidatorTests
{
    private readonly IssueImpersonationTokenValidator _sut = new(Settings());

    /// <summary>Verifies the validator rejects a null settings argument.</summary>
    [Fact]
    public void Ctor_NullSettings_ThrowsArgumentNullException_Test()
    {
        // Arrange
        IImpersonationSettings? settings = null;

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IssueImpersonationTokenValidator(settings!)
        );

        // Assert
        Assert.Equal("settings", ex.ParamName);
    }

    /// <summary>Verifies a fully valid command, including the optional members, has no errors.</summary>
    [Fact]
    public void Validate_ValidCommand_ProducesNoErrors_Test()
    {
        // Arrange
        var command = Command(roles: ["Support"], ticket: "SUP-1", lifetime: MaxMinutes);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Verifies a null, empty or whitespace target fails the target rule.</summary>
    /// <param name="target">The invalid target.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingTarget_FailsTargetRule_Test(string? target)
    {
        // Arrange
        // Act
        var result = _sut.TestValidate(Command(target: target));

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TargetUserId);
    }

    /// <summary>Verifies a target over 200 characters (after trimming) fails, and exactly 200 passes.</summary>
    [Fact]
    public void Validate_TargetLengthBoundary_FailsOnlyOver200_Test()
    {
        // Arrange
        // Act
        var tooLong = _sut.TestValidate(Command(target: new string('a', 201)));
        var atLimit = _sut.TestValidate(Command(target: new string('a', 200)));

        // Assert
        Assert.Multiple(
            () => tooLong.ShouldHaveValidationErrorFor(x => x.TargetUserId),
            () => atLimit.ShouldNotHaveValidationErrorFor(x => x.TargetUserId)
        );
    }

    /// <summary>Verifies control characters in the target, reason, ticket or a role are rejected.</summary>
    [Fact]
    public void Validate_ControlCharacters_AreRejectedEverywhereTextIsAccepted_Test()
    {
        // Arrange
        // Act
        var target = _sut.TestValidate(Command(target: "ali\nce"));
        var reason = _sut.TestValidate(Command(reason: "a long enough reason\r\nforged"));
        var ticket = _sut.TestValidate(Command(ticket: "SUP\t1"));
        var role = _sut.TestValidate(Command(roles: ["Sup\u0001port"]));

        // Assert
        Assert.Multiple(
            () => target.ShouldHaveValidationErrorFor(x => x.TargetUserId),
            () => reason.ShouldHaveValidationErrorFor(x => x.Reason),
            () => ticket.ShouldHaveValidationErrorFor(x => x.TicketReference),
            () => Assert.NotEmpty(role.Errors)
        );
    }

    /// <summary>Verifies impersonating yourself is a validation error on the target, compared after trimming.</summary>
    /// <param name="target">The caller's own id, with or without padding.</param>
    [Theory]
    [InlineData(AdminId)]
    [InlineData("  root  ")]
    public void Validate_TargetIsTheCaller_FailsTargetRule_Test(string target)
    {
        // Arrange
        // Act
        var result = _sut.TestValidate(Command(target: target));

        // Assert
        result
            .ShouldHaveValidationErrorFor(x => x.TargetUserId)
            .WithErrorMessage(
                "'Target User Id' must differ from the caller's own id: impersonating yourself is pointless."
            );
    }

    /// <summary>Verifies more than ten roles fails, ten passes.</summary>
    [Fact]
    public void Validate_RoleCount_FailsOnlyOverTen_Test()
    {
        // Arrange
        // Act
        var tooMany = _sut.TestValidate(
            Command(roles: [.. Enumerable.Range(0, 11).Select(i => $"r{i}")])
        );
        var atLimit = _sut.TestValidate(
            Command(roles: [.. Enumerable.Range(0, 10).Select(i => $"r{i}")])
        );

        // Assert
        Assert.Multiple(
            () => tooMany.ShouldHaveValidationErrorFor(x => x.Roles),
            () => atLimit.ShouldNotHaveValidationErrorFor(x => x.Roles)
        );
    }

    /// <summary>Verifies an empty, whitespace or over-long role name fails.</summary>
    /// <param name="role">The invalid role.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123x")]
    public void Validate_InvalidRoleName_Fails_Test(string role)
    {
        // Arrange
        // Act
        var result = _sut.TestValidate(Command(roles: [role]));

        // Assert
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>Verifies a missing, whitespace or shorter-than-ten-character (after trimming) reason fails the reason rule.</summary>
    /// <param name="reason">The invalid reason.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("          ")]
    [InlineData("too short")]
    [InlineData("   nine chr   ")]
    public void Validate_MissingOrShortReason_FailsReasonRule_Test(string? reason)
    {
        // Arrange
        // Act
        var result = _sut.TestValidate(Command(reason: reason));

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    /// <summary>Verifies the reason boundaries: exactly ten and exactly 500 characters pass, 501 fails.</summary>
    [Fact]
    public void Validate_ReasonLengthBoundaries_AreTenToFiveHundred_Test()
    {
        // Arrange
        // Act
        var atMinimum = _sut.TestValidate(Command(reason: new string('a', 10)));
        var atMaximum = _sut.TestValidate(Command(reason: new string('a', 500)));
        var overMaximum = _sut.TestValidate(Command(reason: new string('a', 501)));

        // Assert
        Assert.Multiple(
            () => atMinimum.ShouldNotHaveValidationErrorFor(x => x.Reason),
            () => atMaximum.ShouldNotHaveValidationErrorFor(x => x.Reason),
            () => overMaximum.ShouldHaveValidationErrorFor(x => x.Reason)
        );
    }

    /// <summary>Verifies a ticket reference is optional, at most 100 characters.</summary>
    [Fact]
    public void Validate_TicketReference_IsOptionalAndCappedAt100_Test()
    {
        // Arrange
        // Act
        var absent = _sut.TestValidate(Command(ticket: null));
        var atLimit = _sut.TestValidate(Command(ticket: new string('t', 100)));
        var over = _sut.TestValidate(Command(ticket: new string('t', 101)));

        // Assert
        Assert.Multiple(
            () => absent.ShouldNotHaveValidationErrorFor(x => x.TicketReference),
            () => atLimit.ShouldNotHaveValidationErrorFor(x => x.TicketReference),
            () => over.ShouldHaveValidationErrorFor(x => x.TicketReference)
        );
    }

    /// <summary>Verifies a non-positive lifetime or one above the configured maximum fails, and the bounds 1 and the maximum pass.</summary>
    /// <param name="minutes">The requested lifetime.</param>
    /// <param name="valid">Whether it should be accepted.</param>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(MaxMinutes, true)]
    [InlineData(MaxMinutes + 1, false)]
    public void Validate_LifetimeMinutes_MustBeBetweenOneAndConfiguredMaximum_Test(
        int minutes,
        bool valid
    )
    {
        // Arrange
        // Act
        var result = _sut.TestValidate(Command(lifetime: minutes));

        // Assert
        if (valid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.LifetimeMinutes);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.LifetimeMinutes);
        }
    }

    /// <summary>Verifies an absent lifetime is valid (the default applies later).</summary>
    [Fact]
    public void Validate_AbsentLifetime_IsValid_Test()
    {
        // Arrange
        // Act
        var result = _sut.TestValidate(Command(lifetime: null));

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.LifetimeMinutes);
    }
}
