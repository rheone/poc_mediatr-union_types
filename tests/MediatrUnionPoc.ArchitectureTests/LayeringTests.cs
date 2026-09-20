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
    private const string ApplicationNamespace = "MediatrUnionPoc.Application";
    private const string InfrastructureNamespace = "MediatrUnionPoc.Infrastructure";
    private const string ApiNamespace = "MediatrUnionPoc.Api";
    private const string EntityFrameworkCoreNamespace = "Microsoft.EntityFrameworkCore";
    private const string MediatRNamespace = "MediatR";
    private const string AspNetCoreMvcNamespace = "Microsoft.AspNetCore.Mvc";

    private static readonly Assembly DomainAssembly = typeof(Product).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(MediatrUnionPoc.Application.DependencyInjection).Assembly;

    private static readonly Assembly InfrastructureAssembly = typeof(EfCoreUnitOfWork).Assembly;

    /// <summary>
    /// Verifies the Domain assembly has no type that depends on the Application, Infrastructure,
    /// or Api assemblies — Domain is meant to sit at the bottom of the dependency graph with no
    /// dependency on any other project in this solution.
    /// </summary>
    [Fact]
    public void Domain_DependsOnOtherProjects_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies the Application assembly has no type that depends on the Infrastructure or Api
    /// assemblies — Application's command/query handlers depend on Domain and on the abstractions
    /// in <c>Common/</c> only, never on a specific persistence or presentation implementation.
    /// </summary>
    [Fact]
    public void Application_DependsOnInfrastructureOrApi_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies the Infrastructure assembly has no type that depends on the Api assembly —
    /// persistence adapters implement Application's abstractions and must not know about the
    /// controllers that consume them.
    /// </summary>
    [Fact]
    public void Infrastructure_DependsOnApi_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace)
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies only the Infrastructure assembly references EF Core — CLAUDE.md calls out that
    /// Domain stays free of an EF Core reference on purpose (hand-written <c>ValueConverter</c>s
    /// instead of Vogen's own generated one), and that intent extends to Application, which has no
    /// reason to know about the persistence technology behind <c>IUnitOfWork</c>/<c>IProductRepository</c>.
    /// </summary>
    [Fact]
    public void DomainAndApplication_DependsOnEntityFrameworkCore_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssemblies([DomainAssembly, ApplicationAssembly])
            .ShouldNot()
            .HaveDependencyOn(EntityFrameworkCoreNamespace)
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies the Domain assembly does not depend on MediatR — the entity and value objects are
    /// independent of the request/response pipeline that only Application and Api participate in.
    /// </summary>
    [Fact]
    public void Domain_DependsOnMediatR_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(MediatRNamespace)
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies only the Api assembly references ASP.NET Core MVC — translating a union into an
    /// HTTP status is the controller's job alone, so no lower layer may know about
    /// <c>IActionResult</c> or controller types.
    /// </summary>
    [Fact]
    public void DomainApplicationAndInfrastructure_DependsOnAspNetCoreMvc_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssemblies([DomainAssembly, ApplicationAssembly, InfrastructureAssembly])
            .ShouldNot()
            .HaveDependencyOn(AspNetCoreMvcNamespace)
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies neither Domain nor Application references a JWT or identity-token library — the
    /// Application layer decides whether an impersonation token may be issued through an abstraction,
    /// and signing lives in the Api project alone.
    /// </summary>
    [Fact]
    public void DomainAndApplication_DependsOnJwtLibraries_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssemblies([DomainAssembly, ApplicationAssembly])
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.IdentityModel", "System.IdentityModel.Tokens")
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    /// <summary>
    /// Verifies neither Domain nor Application references Serilog or the ASP.NET Core HTTP pipeline —
    /// the audit stream is an <c>IAuditLog</c> abstraction in Application, and the file implementation,
    /// the request context and the middleware live in the Api project alone.
    /// </summary>
    [Fact]
    public void DomainAndApplication_DependsOnSerilogOrAspNetCoreHttp_HasNone_Test()
    {
        // Arrange
        // Act
        var result = Types
            .InAssemblies([DomainAssembly, ApplicationAssembly])
            .ShouldNot()
            .HaveDependencyOnAny("Serilog", "Microsoft.AspNetCore.Http")
            .GetResult();

        // Assert
        AssertNoViolations(result);
    }

    // Names the offending types so a failure identifies the violated boundary directly.
    private static void AssertNoViolations(NetArchTest.Rules.TestResult result) =>
        Assert.True(
            result.IsSuccessful,
            "Layering violated by: " + string.Join(", ", result.FailingTypeNames ?? [])
        );
}
