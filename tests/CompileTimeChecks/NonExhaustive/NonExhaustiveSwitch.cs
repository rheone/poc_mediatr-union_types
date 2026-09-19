using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;

namespace CompileTimeChecks.NonExhaustive;

public static class Probe
{
    // The Error case is intentionally left unhandled. This must fail to build with CS8509
    // ("the switch expression does not handle all possible values ... it is not exhaustive").
    public static string Describe(CreateProductResult result) =>
        result switch
        {
            ProductDto dto => "ok",
            ValidationErrors errors => "invalid",
        };
}
