# TheoryData Reference

## Converting `IEnumerable<object[]>` to `TheoryData<>`

### Simple static rows

**Before:**
```csharp
public static IEnumerable<object[]> Parse_ValidInput_Test_Values()
{
    yield return new object[] { "192.168.0.0/24", true };
    yield return new object[] { "not-a-subnet",   false };
    yield return new object[] { null,             false };
}
```

**After:**
```csharp
public static TheoryData<string, bool> Parse_ValidInput_Test_Values =>
    new TheoryData<string, bool>
    {
        { "192.168.0.0/24", true  },
        { "not-a-subnet",   false },
        { null,             false },
    };
```

Use the expression-bodied property form when all rows are static literals. The type parameters must match the test method's parameter types exactly.

### Rows that require local computation

When rows depend on objects constructed at runtime, use a `get` block with explicit `data.Add(...)` calls. **Never use collection initializer syntax with locally-declared variables — it produces CS1525 compiler errors on all target frameworks.**

**Before:**
```csharp
public static IEnumerable<object[]> Ctor_IPAddress_Int_Test_Values()
{
    foreach (var ip in new[] { IPAddress.Any, IPAddress.Loopback })
        for (var prefix = 0; prefix <= 32; prefix++)
            yield return new object[] { ip, prefix, new Subnet(ip, prefix) };
}
```

**After — `get` block with `data.Add()`:**
```csharp
public static TheoryData<IPAddress, int, Subnet> Ctor_IPAddress_Int_Test_Values
{
    get
    {
        var data = new TheoryData<IPAddress, int, Subnet>();

        foreach (var ip in new[] { IPAddress.Any, IPAddress.Loopback })
            for (var prefix = 0; prefix <= 32; prefix++)
                data.Add(ip, prefix, new Subnet(ip, prefix));

        return data;
    }
}
```

The `get` block is required whenever any row value is a locally-declared variable. The expression-bodied form (`=>`) cannot reference locals.

---

## Deduplicating Loop-Generated Theory Data

When a loop feeds values through a **normalizing constructor**, multiple source inputs can produce the same output. xUnit identifies theory rows by their serialized display name — rows that serialize identically are silently skipped.

### Preferred fix: source reduction

Choose one canonical input per equivalence class instead of iterating over inputs that normalize to the same output. This is the clearest fix because it makes the data set's intent explicit.

**Problematic — multiple IPs normalize to the same subnet at low prefix lengths:**
```csharp
// IPAddress.Any (0.0.0.0) and IPAddress.Loopback (127.0.0.1) both produce 0.0.0.0/0
// at prefix 0, 0.0.0.0/1 at prefix 1, etc. — duplicates at every shared prefix.
foreach (var ip in new[] { IPAddress.Any, IPAddress.Loopback, IPAddress.Parse("192.168.1.1") })
    for (var prefix = 0; prefix <= 32; prefix++)
        data.Add(new Subnet(ip, prefix));
```

**Fixed — one canonical IP per address family:**
```csharp
// One IPv4 representative covers all prefix lengths without duplication.
foreach (var prefix in Enumerable.Range(0, 33))
    data.Add(new Subnet(IPAddress.Parse("10.0.0.1"), prefix));
```

### Fallback: HashSet dedup on the serialized form

Use this only when the full cross-product of inputs is genuinely needed for coverage and source reduction would drop meaningful cases.

Key selection rule: **use the serialized form of the output value** (what xUnit will display), not the raw input. Using the raw input as the key allows collisions through; using the output catches them.

```csharp
public static TheoryData<Subnet> AllPrefixLengths_Test_Values
{
    get
    {
        var data = new TheoryData<Subnet>();
        var seen = new HashSet<string>();

        foreach (var ip in new[] { IPAddress.Any, IPAddress.Loopback, IPAddress.Parse("192.168.1.1") })
        {
            for (var prefix = 0; prefix <= 32; prefix++)
            {
                var subnet = new Subnet(ip, prefix);
                // Key on the registered serializer's output — same key xUnit uses for display names.
                if (seen.Add(subnet.ToString("f", null)))
                    data.Add(subnet);
            }
        }

        return data;
    }
}
```

The key format must match how xUnit serializes the type. For types with a registered `IXunitSerializer`, use the same format string the serializer uses. For `ToString()`-based types, use the default `ToString()`. When in doubt, run `--list-tests` and inspect the actual display names.

---

## Choosing the Right Key Format

| Type | Registered serializer? | Key to use |
|------|------------------------|------------|
| `Subnet` | Yes (`SubnetXunitSerializer`, format `"f"`) | `subnet.ToString("f", null)` |
| `IPAddressRange` | Yes (`IPAddressRangeXunitSerializer`, format `"G"`) | `range.ToString("G", null)` |
| `IPAddress` | No | `address.ToString()` |
| Primitive (`int`, `string`, `bool`) | Built-in | Value itself as string |
| Custom type with `IXunitSerializer` | Yes | Match the serializer's `Serialize()` output |

---

## Parse-Roundtrip Data

When theory rows provide inputs to a **parse method**, the inputs must use the same normalized form the expected value uses. Using a raw pre-normalization source breaks the round-trip.

**Broken — raw IP doesn't match the normalized subnet head:**
```csharp
var subnet = new Subnet(IPAddress.Parse("192.168.1.1"), 8);
// subnet.Head = "192.0.0.0", but low = "192.168.1.1"
// Subnet.Parse("192.168.1.1", "192.255.255.255") builds 192.128.0.0/9, not 192.0.0.0/8
var low  = ipAddress.ToString();   // "192.168.1.1" — wrong
var high = subnet.Tail.ToString(); // "192.255.255.255"
data.Add(subnet, low, high);
```

**Fixed — derive inputs from the normalized expected value:**
```csharp
var subnet = new Subnet(IPAddress.Parse("192.168.1.1"), 8);
var low  = subnet.Head.ToString(); // "192.0.0.0" — matches what Parse expects
var high = subnet.Tail.ToString(); // "192.255.255.255"
data.Add(subnet, low, high);
```

Rule: **the `expected` value and the parse inputs must be consistent with each other**, not with the raw pre-normalization source. Always derive inputs from `expected`, not from the source that was used to construct `expected`.
