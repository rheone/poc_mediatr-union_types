# STL 3D models

GitHub renders **interactive 3D models** from **ASCII STL** inside a fenced
code block tagged `stl`. Works in issues, PRs, discussions, wikis, and `.md`
files. (Binary STL is not supported inline — ASCII only.)

## What ASCII STL looks like

STL describes a solid as a list of triangles (`facet`s), each with a normal and
three vertices. Minimal example — a pyramid built from four facets:

````markdown
```stl
solid cube_corner
  facet normal 0.0 -1.0 0.0
    outer loop
      vertex 0.0 0.0 0.0
      vertex 1.0 0.0 0.0
      vertex 0.0 0.0 1.0
    endloop
  endfacet
  facet normal 0.0 0.0 -1.0
    outer loop
      vertex 0.0 0.0 0.0
      vertex 0.0 1.0 0.0
      vertex 1.0 0.0 0.0
    endloop
  endfacet
  facet normal -1.0 0.0 0.0
    outer loop
      vertex 0.0 0.0 0.0
      vertex 0.0 0.0 1.0
      vertex 0.0 1.0 0.0
    endloop
  endfacet
  facet normal 0.577 0.577 0.577
    outer loop
      vertex 1.0 0.0 0.0
      vertex 0.0 1.0 0.0
      vertex 0.0 0.0 1.0
    endloop
  endfacet
endsolid
```
````

## Rendered view

The result is an interactive viewer with **Wireframe**, **Surface Angle**, and
**Solid** display modes you can toggle.

## Practical guidance

- **Don't hand-write STL.** For anything beyond a trivial shape, export STL
  from a CAD/3D tool and paste the ASCII export into the fence.
- **Keep it small** — model files bloat the page and renderer. Simplify or
  decimate the mesh first.
- The `solid …` name line is free-form (here `cube_corner`); it does not affect
  rendering.
- Each `facet` must close with `endfacet`; each `outer loop` with `endloop`;
  the whole solid with `endsolid`.

## .stl files

`.stl` files in a repository render in GitHub's 3D file viewer when opened.
Inline fenced STL gives the same viewer without adding a binary/ASCII file to
the repo.

## Pragmatic rules

1. Fence tag must be exactly `stl` (lower-case).
2. Use ASCII STL — binary STL does not render inline.
3. Generate from a tool rather than by hand; validate in a local STL viewer.
4. Keep vertex counts modest for large inline models.