// ControllerBase (and Minimal API result helpers) reserve names like NotFound/Ok/BadRequest as
// methods, which collide with case-type names chosen for readability inside the Application layer.
// Solved once, here, rather than re-discovered and locally aliased by every new controller that
// wants to pattern-match on these case types.
global using NotFoundCase = MediatrUnionPoc.Application.Common.Results.NotFound<MediatrUnionPoc.Domain.ProductId>;
