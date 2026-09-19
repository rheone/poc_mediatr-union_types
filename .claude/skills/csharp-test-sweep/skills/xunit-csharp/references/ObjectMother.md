# Reference: Object Mother Pattern

An Object Mother is a static factory class in the test project that returns **named, canonical instances** of a domain type. Use it when the same pre-built values appear in multiple tests and constructing them inline is repetitive or noisy.

Object mother should be stateless, and provide either immutable data, or a new instance of data each time to avoid side effects.

## When to Use

| Situation | Use Object Mother? |
| --------- | ------------------ |
| Same value constructed identically in 3+ tests | Yes |
| Value has a meaningful name in the domain ("a typical IPv4 subnet", "a host route") | Yes |
| Type is trivially constructable with one line | No — construct inline |
| Tests need slight variations of the same base object | No — use theory data or constructor arguments |
| The value is the subject of the assertion | No — pass explicitly so intent is clear |

## Placement

- **One test class uses it:** place the file alongside that test class.
- **Multiple test classes use it:** place it under a `TestData/` subfolder in the test project. Name the file `{Type}Mother.cs`.

```
src/
  MyProject.Tests/
    SubnetTests.cs
    IPAddressRangeTests.cs
    TestData/
      SubnetMother.cs        ← shared across SubnetTests and IPAddressRangeTests
    ComparerTests/
      DefaultIPAddressComparerTests.cs
      ComparerMother.cs      ← only used in this folder, colocated
```

## Example — `SubnetMother`

```csharp
// TestData/SubnetMother.cs
internal static class SubnetMother
{
    /// <summary>A typical /24 IPv4 subnet: 192.168.0.0/24 (256 addresses).</summary>
    public static Subnet TypicalIPv4()      => Subnet.Parse("192.168.0.0/24");

    /// <summary>The entire IPv4 address space: 0.0.0.0/0.</summary>
    public static Subnet FullIPv4()         => Subnet.Parse("0.0.0.0/0");

    /// <summary>A single-host IPv4 route: 10.0.0.1/32 (1 address).</summary>
    public static Subnet SingleHostIPv4()   => Subnet.Parse("10.0.0.1/32");

    /// <summary>A typical /64 IPv6 subnet.</summary>
    public static Subnet TypicalIPv6()      => Subnet.Parse("::/64");

    /// <summary>The entire IPv6 address space: ::/0.</summary>
    public static Subnet FullIPv6()         => Subnet.Parse("::/0");
}
```

## Example — Using the Mother in Tests

```csharp
public sealed class SubnetTests
{
    #region Contains

    /// <summary>Verifies Contains returns true when the address is within the subnet.</summary>
    [Fact]
    public void Contains_AddressWithinSubnet_ReturnsTrue_Test()
    {
        // Arrange
        var subnet  = SubnetMother.TypicalIPv4();
        var address = IPAddress.Parse("192.168.0.100");

        // Act
        var result = subnet.Contains(address);

        // Assert
        Assert.True(result);
    }

    /// <summary>Verifies Contains returns false when the address is outside the subnet.</summary>
    [Fact]
    public void Contains_AddressOutsideSubnet_ReturnsFalse_Test()
    {
        // Arrange
        var subnet  = SubnetMother.TypicalIPv4();
        var address = IPAddress.Parse("10.0.0.1");

        // Act
        var result = subnet.Contains(address);

        // Assert
        Assert.False(result);
    }

    #endregion
}
```

## Example — Mother Used Across Classes

```csharp
// SubnetTests.cs
public sealed class SubnetTests
{
    [Fact]
    public void Overlaps_WithSameSubnet_ReturnsTrue_Test()
    {
        var a = SubnetMother.TypicalIPv4();
        var b = SubnetMother.TypicalIPv4();
        Assert.True(a.Overlaps(b));
    }
}

// DefaultIPAddressRangeComparerTests.cs
public sealed class DefaultIPAddressRangeComparerTests
{
    [Fact]
    public void Compare_TwoIdenticalSubnets_ReturnsZero_Test()
    {
        var comparer = new DefaultIIPAddressRangeComparer();
        var result   = comparer.Compare(SubnetMother.TypicalIPv4(), SubnetMother.TypicalIPv4());
        Assert.Equal(0, result);
    }
}
```

## What NOT to Do

```csharp
// BAD — Object Mother substituting for theory data
// If the test cares about specific addresses, pass them explicitly
[Fact]
public void Parse_SpecificInput_ReturnsExpected_Test()
{
    // Don't hide the input behind a mother if the value IS the point of the test
    var subnet = SubnetMother.TypicalIPv4(); // the reader has to look up what this is
    Assert.Equal("192.168.0.0", subnet.NetworkAddress.ToString());
}

// GOOD — value is explicit when it's what's being asserted
[Fact]
public void Parse_SpecificInput_ReturnsExpected_Test()
{
    var subnet = Subnet.Parse("192.168.0.0/24");
    Assert.Equal("192.168.0.0", subnet.NetworkAddress.ToString());
}
```

## Rules Summary

- Methods return **new instances** on each call — never cache or reuse.
- Name methods after domain concepts, not technical descriptions (`TypicalIPv4`, not `GetSubnetWithPrefix24`).
- Add an XML `<summary>` to each method documenting the exact value and why it is canonical.
- Keep the class `internal static` — it has no business in the production assembly.
