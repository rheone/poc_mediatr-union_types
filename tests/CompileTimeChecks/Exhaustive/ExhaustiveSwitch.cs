using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;

namespace CompileTimeChecks.Exhaustive;

public static class Probe
{
    // Same shape as CompileTimeChecks.NonExhaustive.Probe, but every case is handled.
    public static string Describe(CreateProductResult result) =>
        result switch
        {
            ProductDto dto => "ok",
            ValidationErrors errors => "invalid",
            Error error => "error",
        };
}
