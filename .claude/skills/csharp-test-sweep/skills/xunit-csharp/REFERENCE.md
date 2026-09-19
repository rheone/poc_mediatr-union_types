# xUnit v3 Reference

## Non-Serializable Types in Theories

When a type used in `TheoryData<T>` is not natively serializable by xUnit, implement `IXunitSerializer<T>` and register it:

```csharp
internal sealed class SubnetXunitSerializer : IXunitSerializer<Subnet>
{
    public Subnet Deserialize(string serialized) => Subnet.Parse(serialized);
    public string Serialize(Subnet value) => value.ToString();
    public bool IsSerializable(Type type, object? value, out string? failureReason)
    {
        failureReason = type != typeof(Subnet) ? $"{type.FullName} not supported" : null;
        return type == typeof(Subnet);
    }
}

[assembly: RegisterXunitSerializer(typeof(SubnetXunitSerializer), typeof(Subnet))]
```

See [references/IXunitSerializer.md](references/IXunitSerializer.md) for the full pattern including `IXunitSerializable` (v2) → `IXunitSerializer` (v3) migration, multi-type serializers, and round-trip rules.

---

## Assertion Rewrites

| Anti-pattern | Replace with |
|---|---|
| `Assert.True(x == y)` | `Assert.Equal(y, x)` |
| `Assert.True(x != y)` | `Assert.NotEqual(y, x)` |
| `Assert.True(result != null)` | `Assert.NotNull(result)` |
| `Assert.True(result == null)` | `Assert.Null(result)` |
| `Assert.Equal(true, condition)` | `Assert.True(condition)` |
| `Assert.Equal(false, condition)` | `Assert.False(condition)` |
| `Assert.Equal(null, obj)` | `Assert.Null(obj)` |
| `Assert.NotEqual(null, obj)` | `Assert.NotNull(obj)` |
