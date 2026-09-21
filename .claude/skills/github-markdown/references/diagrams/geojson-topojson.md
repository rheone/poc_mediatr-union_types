# GeoJSON / TopoJSON maps

GitHub renders **interactive maps** from GeoJSON or TopoJSON inside a fenced
code block tagged `geojson` or `topojson`. Works in issues, PRs, discussions,
wikis, and `.md` files.

## Minimal GeoJSON

The map is defined by GeoJSON coordinates wrapped in a `geojson` fence:

````markdown
```geojson
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": 1,
      "properties": { "ID": 0 },
      "geometry": {
        "type": "Polygon",
        "coordinates": [
          [
            [-90, 35],
            [-90, 30],
            [-85, 30],
            [-85, 35],
            [-90, 35]
          ]
        ]
      }
    }
  ]
}
```
````

Coordinates are `[longitude, latitude]` pairs; the polygon above draws a box
over the southeastern US.

## TopoJSON

TopoJSON encodes topology via arcs plus a transform; the fence tag is
`topojson`:

````markdown
```topojson
{
  "type": "Topology",
  "transform": {
    "scale": [0.0005000500050005, 0.00010001000100010001],
    "translate": [100, 0]
  },
  "objects": {
    "example": {
      "type": "GeometryCollection",
      "geometries": [
        {
          "type": "Point",
          "properties": { "prop0": "value0" },
          "coordinates": [4000, 5000]
        },
        {
          "type": "LineString",
          "properties": { "prop0": "value0", "prop1": 0 },
          "arcs": [0]
        }
      ]
    }
  },
  "arcs": [
    [[4000, 0], [1999, 9999], [2000, -9999], [2000, 9999]],
    [[0, 0], [0, 9999], [2000, 0], [0, -9999], [-2000, 0]]
  ]
}
```
````

TopoJSON is more compact for shared boundaries; prefer it for large or
multi-feature datasets.

## Supported geometries

GeoJSON `Point`, `LineString`, `Polygon`, `Multi*` variants, and
`FeatureCollection` all render. Each feature can carry `properties` (e.g. an
`ID`) that the map shows on interaction.

## .geojson / .topojson files

Files with `.geojson`/`.topojson` extensions in a repository also render as
interactive maps when viewed. Inline fenced blocks give the same result
without adding files to the repo.

## Pragmatic rules

1. Use a `geojson`/`topojson` fence with valid JSON inside — malformed JSON
   renders as plain code, not a map.
2. Coordinate order is **longitude, latitude** — a common source of maps that
   appear in the wrong place.
3. Prefer `FeatureCollection` for anything with multiple features.
4. Validate GeoJSON with an external validator before pasting; GitHub gives no
   inline map for invalid input.