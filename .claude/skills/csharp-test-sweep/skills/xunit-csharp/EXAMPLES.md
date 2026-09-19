# xUnit v3 Worked Examples

---

## Example 1 — Complete Test Class with TheoryData

```csharp
using System.Numerics;
using Xunit;

public sealed class SubnetTests
{
    #region Parse

    /// <summary>
    /// Inputs: routePrefix (string), expectedLength (BigInteger).
    /// Partitions: valid IPv4 CIDR, valid IPv6 CIDR, prefix at lower boundary (/0),
    /// prefix at upper boundary (/32 IPv4).
    /// </summary>
    public static TheoryData<string, BigInteger> Parse_ValidInput_ReturnsCorrectLength_Test_Data =>
        new()
        {
            { "192.168.0.0/24", 256 },           // valid IPv4 CIDR
            { "::/64",           BigInteger.Parse("18446744073709551616") },  // valid IPv6 CIDR
            { "0.0.0.0/0",       BigInteger.Parse("4294967296") },            // lower boundary
            { "10.0.0.1/32",     1 },              // upper boundary IPv4
        };

    /// <summary>Verifies Parse returns a subnet with the correct length for valid CIDR input.</summary>
    [Theory]
    [MemberData(nameof(Parse_ValidInput_ReturnsCorrectLength_Test_Data))]
    public void Parse_ValidInput_ReturnsCorrectLength_Test(string routePrefix, BigInteger expectedLength)
    {
        // Arrange
        // (inputs come from MemberData)

        // Act
        var result = Subnet.Parse(routePrefix);

        // Assert
        Assert.Equal(expectedLength, result.Length);
    }

    /// <summary>Verifies Parse throws ArgumentNullException for null input.</summary>
    [Fact]
    public void Parse_NullInput_ThrowsArgumentNullException_Test()
    {
        // Arrange
        string? input = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => Subnet.Parse(input!));
        Assert.Contains("input", ex.ParamName);
    }

    #endregion
}
```

---

## Example 2 — Assert.Multiple

```csharp
[Fact]
public void Split_ValidCidr_ReturnsHeadAndTail_Test()
{
    // Arrange
    var subnet = Subnet.Parse("192.168.0.0/24");

    // Act
    var (head, tail) = subnet.Split();

    // Assert — all properties are independent; report every failure at once
    Assert.Multiple(
        () => Assert.Equal(IPAddress.Parse("192.168.0.0"), head.NetworkAddress),
        () => Assert.Equal(IPAddress.Parse("192.168.1.255"), tail.BroadcastAddress),
        () => Assert.Equal(128UL, head.Length)
    );
}
```

---

## Example 3 — Assert.Equivalent

```csharp
public sealed record Route(string Prefix, int Metric);

[Fact]
public void ParseRoute_ReturnsExpectedRoute_Test()
{
    // Arrange
    var input = "192.168.0.0/24 metric 42";

    // Act
    var result = RouteParser.Parse(input);

    // Assert — Route does not override Equals, use structural comparison
    Assert.Equivalent(new Route("192.168.0.0/24", 42), result);
}
```

---

## Example 4 — CancellationToken Test

```csharp
[Fact]
public async Task ProcessAsync_CancelledToken_ThrowsOperationCanceledException_Test()
{
    // Arrange
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    // Act & Assert
    await Assert.ThrowsAsync<OperationCanceledException>(
        () => _sut.ProcessAsync(cts.Token));
}
```

---

## Example 5 — IClassFixture Wiring

```csharp
// Fixture — created once per test class
public sealed class DatabaseFixture : IDisposable
{
    public MyDatabase Database { get; }

    public DatabaseFixture()
    {
        Database = new MyDatabase(":memory:");
        Database.Migrate();
    }

    public void Dispose() => Database.Dispose();
}

// Test class — receives the shared fixture via constructor
public sealed class RepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly MyDatabase _db;

    public RepositoryTests(DatabaseFixture fixture)
    {
        _db = fixture.Database;
    }

    [Fact]
    public void Insert_NewRecord_PersistsToDatabase_Test()
    {
        // Arrange
        var record = new Record { Id = 1, Value = "test" };

        // Act
        _db.Insert(record);

        // Assert
        Assert.Equal(record, _db.Find(1));
    }
}
```

> Note: Shared fixtures mean mutations in one test affect later tests. Either keep tests read-only or reset state between tests.

See [references/Fixtures.md](references/Fixtures.md) for `ICollectionFixture`, `IAsyncLifetime`, and `[CollectionDefinition]` patterns.

---

## Example 6 — Object Mother Usage

```csharp
// TestData/SubnetMother.cs
internal static class SubnetMother
{
    /// <summary>A typical /24 IPv4 subnet: 192.168.0.0/24.</summary>
    public static Subnet TypicalIPv4()    => Subnet.Parse("192.168.0.0/24");

    /// <summary>A single-host route: 10.0.0.1/32.</summary>
    public static Subnet SingleHostIPv4() => Subnet.Parse("10.0.0.1/32");

    /// <summary>The entire IPv4 space: 0.0.0.0/0.</summary>
    public static Subnet FullIPv4()       => Subnet.Parse("0.0.0.0/0");
}

// Test class using the mother
public sealed class SubnetTests
{
    [Fact]
    public void Contains_AddressWithinSubnet_ReturnsTrue_Test()
    {
        var subnet  = SubnetMother.TypicalIPv4();
        var address = IPAddress.Parse("192.168.0.100");

        var result = subnet.Contains(address);

        Assert.True(result);
    }
}
```

See [references/ObjectMother.md](references/ObjectMother.md) for placement rules, multi-class usage, and what NOT to do.
