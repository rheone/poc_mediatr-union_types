using System.Reflection;
using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure;
using NetArchTest.Rules;

namespace MediatrUnionPoc.ArchitectureTests;

/// <summary>
/// Enforces the dependency direction CLAUDE.md documents as intended for this solution's layers
/// — Domain has no dependency on any other project here, Application depends only on Domain,
/// Infrastructure depends on Domain and Application, and Api may depend on all three — regardless
/// of whether every project reference in the .csproj files happens to already respect it.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Product).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(MediatrUnionPoc.Application.DependencyInjection).Assembly;

    private static readonly Assembly InfrastructureAssembly = typeof(InMemoryUnitOfWork).Assembly;

    /// <summary>
    /// Verifies the Domain assembly has no type that depends on the Application, Infrastructure,
    /// or Api assemblies — Domain is meant to sit at the bottom of the dependency graph with no
    /// dependency on any other project in this solution.
    /// </summary>
    [Fact]
    public void Domain_types_do_not_depend_on_application_infrastructure_or_api()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MediatrUnionPoc.Application",
                "MediatrUnionPoc.Infrastructure",
                "MediatrUnionPoc.Api"
            )
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Verifies the Application assembly has no type that depends on the Infrastructure or Api
    /// assemblies — Application's command/query handlers depend on Domain and on the abstractions
    /// in <c>Common/</c> only, never on a specific persistence or presentation implementation.
    /// </summary>
    [Fact]
    public void Application_types_do_not_depend_on_infrastructure_or_api()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("MediatrUnionPoc.Infrastructure", "MediatrUnionPoc.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Verifies the Infrastructure assembly has no type that depends on the Api assembly —
    /// persistence adapters implement Application's abstractions and must not know about the
    /// controllers that consume them.
    /// </summary>
    [Fact]
    public void Infrastructure_types_do_not_depend_on_api()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("MediatrUnionPoc.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Verifies only the Infrastructure assembly references EF Core — CLAUDE.md calls out that
    /// Domain stays free of an EF Core reference on purpose (hand-written <c>ValueConverter</c>s
    /// instead of Vogen's own generated one), and that intent extends to Application, which has no
    /// reason to know about the persistence technology behind <c>IUnitOfWork</c>/<c>IProductRepository</c>.
    /// </summary>
    [Fact]
    public void Only_infrastructure_depends_on_entity_framework_core()
    {
        var result = Types
            .InAssemblies([DomainAssembly, ApplicationAssembly])
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
