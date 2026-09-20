using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>Builds substituted <see cref="IUnitOfWork"/>s whose commit outcome is explicit rather than a default.</summary>
internal static class UnitOfWorkMother
{
    /// <summary>Creates a unit of work whose <see cref="IUnitOfWork.CommitAsync"/> reports <see cref="Committed"/>.</summary>
    /// <returns>The substitute.</returns>
    public static IUnitOfWork Committing()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork
            .CommitAsync(Arg.Any<CancellationToken>())
            .Returns(new CommitResult(new Committed()));
        return unitOfWork;
    }
}
