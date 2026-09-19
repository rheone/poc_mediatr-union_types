# Reference: `IXunitSerializer` Pattern (xUnit v3)

Use this pattern when a type used in `TheoryData<T>` or `[MemberData]` is not natively serializable by xUnit (e.g. domain objects, custom structs, types without a parameterless constructor).

## When You Need This

xUnit v3 serializes theory data so each test case appears as a distinct entry in test runners. If a type is not serializable, xUnit cannot distinguish test cases and will report them all under one name. You will see a warning like:

> `Could not serialize value of type 'MyType' for display`

## Step 1 — Implement `IXunitSerializer<T>`

```csharp
using Xunit.Sdk;

internal sealed class SubnetXunitSerializer : IXunitSerializer<Subnet>
{
    /// <inheritdoc />
    public Subnet Deserialize(string serialized) =>
        Subnet.Parse(serialized);

    /// <inheritdoc />
    public string Serialize(Subnet value) =>
        value.ToString(); // must produce a round-trippable string

    /// <inheritdoc />
    public bool IsSerializable(Type type, object? value, out string? failureReason)
    {
        if (type == typeof(Subnet))
        {
            failureReason = null;
            return true;
        }

        failureReason = $"{type.FullName} is not supported by {nameof(SubnetXunitSerializer)}";
        return false;
    }
}
```

**Rules:**
- `Serialize` and `Deserialize` must be inverse operations: `Deserialize(Serialize(x))` must equal `x`.
- Keep the serialized form human-readable — it appears in test runner output.
- One serializer per type is sufficient; a single serializer can handle multiple related types by checking `type` in `IsSerializable`.

## Step 2 — Register with `[assembly: RegisterXunitSerializer]`

Place the attribute in any `.cs` file in the test project (conventionally in `AssemblyInfo.cs` or a dedicated `XunitSerializers.cs`):

```csharp
[assembly: RegisterXunitSerializer(typeof(SubnetXunitSerializer), typeof(Subnet))]
```

Register one attribute per serialized type.

## Step 3 — Use in Theory Data Normally

No changes needed at the `TheoryData` or `[MemberData]` site — xUnit picks up the serializer automatically:

```csharp
public static TheoryData<Subnet, bool> Contains_Test_Data =>
    new()
    {
        { Subnet.Parse("192.168.0.0/24"), true  },
        { Subnet.Parse("10.0.0.0/8"),     false },
    };

[Theory]
[MemberData(nameof(Contains_Test_Data))]
public void Contains_KnownSubnet_ReturnsExpected_Test(Subnet subnet, bool expected) { ... }
```

## Full Example — Multiple Types in One File

```csharp
// XunitSerializers.cs
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(SubnetXunitSerializer),         typeof(Subnet))]
[assembly: RegisterXunitSerializer(typeof(IPAddressRangeXunitSerializer), typeof(IPAddressRange))]

internal sealed class SubnetXunitSerializer : IXunitSerializer<Subnet>
{
    public Subnet Deserialize(string serialized) => Subnet.Parse(serialized);
    public string Serialize(Subnet value) => value.ToString();
    public bool IsSerializable(Type type, object? value, out string? failureReason)
    {
        failureReason = type != typeof(Subnet)
            ? $"{type.FullName} not supported"
            : null;
        return type == typeof(Subnet);
    }
}

internal sealed class IPAddressRangeXunitSerializer : IXunitSerializer<IPAddressRange>
{
    public IPAddressRange Deserialize(string serialized) => IPAddressRange.Parse(serialized);
    public string Serialize(IPAddressRange value) => value.ToString();
    public bool IsSerializable(Type type, object? value, out string? failureReason)
    {
        failureReason = type != typeof(IPAddressRange)
            ? $"{type.FullName} not supported"
            : null;
        return type == typeof(IPAddressRange);
    }
}
```

## Migration Note: `IXunitSerializable` (v2) → `IXunitSerializer` (v3)

| xUnit v2 | xUnit v3 |
| -------- | -------- |
| Implement `IXunitSerializable` on the type itself | Implement `IXunitSerializer<T>` in a separate serializer class |
| No registration required | `[assembly: RegisterXunitSerializer(...)]` required |
| `Populate` / `GetData` methods | `Serialize` / `Deserialize` / `IsSerializable` methods |

Do not implement `IXunitSerializable` on production types — it bleeds test infrastructure into the production assembly.
