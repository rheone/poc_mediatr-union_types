# Multi-Target Projects

Read when `<TargetFrameworks>` includes `net4x` or `netstandard`.

## Lowest common denominator

Write tests at the lowest target framework and C# language version in the list (`net4x` compiles at C# 7.3 by default, so `record`, switch expressions, `using` declarations, target-typed `new()`, and inferred delegate types fail there). Build with `dotnet build --framework net48` and let the compiler report violations. Add `#if` only when:

1. The API under test does not exist on all targets
2. An assertion helper or type used in the test does not exist on all targets
3. A behavior difference fails the test on a legitimate target

When unsure whether a difference exists, write the test without guards and mark the uncertainty `// SWEEP-AMBIGUITY:`.

## `#if` vs `[Skip]` vs a separate method

| Situation | Approach |
|-----------|----------|
| API exists everywhere but behaves differently | `#if` around the differing assertion |
| API exists on some targets only | `#if` around the whole test |
| Test infrastructure missing on a target (e.g. `BinaryFormatter` on net8+) | `#if` around the test |
| Slow or broken on one target | `[Skip]` with justification |
| Difference so large a separate test reads clearer | Separate method, `#if` excludes the other |

Swap the differing value with `#if`; one test serves every target.

```csharp
#if NET8_0_OR_GREATER
    Assert.Equal("192.168.0.0/24", result, StringComparer.Ordinal);
#else
    Assert.Equal("192.168.0.0/24", result);
#endif
```

Symbols follow `NET{major}_{minor}_OR_GREATER`, plus `NETFRAMEWORK`, `NET48`, `NETSTANDARD2_0`, `NETSTANDARD2_1_OR_GREATER`.

## Batching net4x runs

Without a .NET Framework runner, collect one command per modified class and emit them together at end of sweep, with the full project path so they run from any directory:

```shell
dotnet test ./src/MyLib.Tests --framework net48 --filter "FullyQualifiedName~SubnetTests"
```
