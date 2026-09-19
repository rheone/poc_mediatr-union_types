# NUnit v5 Examples

## 1. Complete Test Class — Constraint Model

```csharp
[TestFixture]
public sealed class SubnetParserTests
{
    private SubnetParser _parser;

    [SetUp]
    public void SetUp()
    {
        _parser = new SubnetParser();
    }

    [Test]
    public void Parse_ValidCidr_ReturnsSubnet_Test()
    {
        // Arrange
        var input = "192.168.0.0/24";

        // Act
        var result = _parser.Parse(input);

        // Assert
        Assert.That(result.NetworkAddress, Is.EqualTo(IPAddress.Parse("192.168.0.0")));
        Assert.That(result.PrefixLength, Is.EqualTo(24));
    }
}
```

## 2. Parameterized Tests — `[TestCase]`

```csharp
[TestCase("192.168.0.0/24", "192.168.0.0", 24)]
[TestCase("10.0.0.0/8", "10.0.0.0", 8)]
[TestCase("0.0.0.0/0", "0.0.0.0", 0)]
public void Parse_ValidCidr_ReturnsCorrectSubnet_Test(
    string input, string expectedAddress, int expectedPrefix)
{
    var result = Subnet.Parse(input);

    Assert.That(result.NetworkAddress, Is.EqualTo(IPAddress.Parse(expectedAddress)));
    Assert.That(result.PrefixLength, Is.EqualTo(expectedPrefix));
}
```

## 3. Exception Assertions — `Throws.InstanceOf`

```csharp
[TestCase(null)]
[TestCase("")]
[TestCase("   ")]
public void Parse_InvalidInput_ThrowsArgumentNullException_Test(string input)
{
    Assert.That(() => Subnet.Parse(input), Throws.InstanceOf<ArgumentNullException>());
}
```

With message verification:

```csharp
[Test]
public void Parse_NullInput_ThrowsArgumentNullExceptionWithParamName_Test()
{
    var ex = Assert.Throws<ArgumentNullException>(() => Subnet.Parse(null));
    Assert.That(ex.ParamName, Is.EqualTo("input"));
}
```

## 4. `Assert.Multiple` Batched Verification

```csharp
[Test]
public void Parse_ValidCidr_ReturnsCorrectProperties_Test()
{
    var result = Subnet.Parse("192.168.0.0/24");

    Assert.Multiple(() =>
    {
        Assert.That(result.NetworkAddress, Is.EqualTo(IPAddress.Parse("192.168.0.0")));
        Assert.That(result.PrefixLength, Is.EqualTo(24));
        Assert.That(result.TotalAddresses, Is.EqualTo(256));
    });
}
```

Without `Assert.Multiple`, only the first failure is reported and subsequent assertions are skipped.

## 5. Constraint Composition with `&` and `|`

```csharp
[Test]
public void Parse_ValidCidr_ReturnsNonNullAndHasPrefix_Test()
{
    var result = Subnet.Parse("10.0.0.0/8");

    Assert.That(result, Is.Not.Null & Has.Property("PrefixLength").EqualTo(8));
}

[Test]
public void GetClassification_ExtremeValues_ReturnsExpected_Test()
{
    var classification = classifier.Get(0);

    Assert.That(classification, Is.EqualTo("Boundary") | Is.EqualTo("MinValue"));
}
```

## 6. `[SetUp]` / `[TearDown]` Fixture

```csharp
[TestFixture]
public sealed class RepositoryTests
{
    private InMemoryRepository _repo;

    [SetUp]
    public void SetUp()
    {
        _repo = new InMemoryRepository();
        _repo.Seed(); // fresh state per test
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [Test]
    public void FindById_ExistingItem_ReturnsItem_Test() { /* ... */ }

    [Test]
    public void FindById_MissingItem_ReturnsNull_Test() { /* ... */ }
}
```

## 7. `[OneTimeSetUp]` for Expensive Setup

```csharp
[TestFixture]
public sealed class DatabaseIntegrationTests
{
    private static DatabaseContainer _container;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _container = new DatabaseContainer();
        _container.Start(); // starts a testcontainer once
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _container.Dispose();
    }

    [Test]
    public void Query_ExistingRecord_ReturnsData_Test() { /* ... */ }
}
```

## 8. Async Test

```csharp
[Test]
public async Task ProcessAsync_ValidInput_ReturnsResult_Test()
{
    var result = await _service.ProcessAsync("valid");

    Assert.That(result, Is.Not.Null);
    Assert.That(result.Status, Is.EqualTo("Processed"));
}

[Test]
public void ProcessAsync_NullInput_ThrowsArgumentNullException_Test()
{
    Assert.That(
        async () => await _service.ProcessAsync(null),
        Throws.InstanceOf<ArgumentNullException>());
}
```

## 9. `[TestCaseSource]` with `TestCaseData` Named Parameters

```csharp
[TestCaseSource(nameof(Parse_InvalidCidr_ThrowsException_Test_Data))]
public void Parse_InvalidCidr_ThrowsException_Test(string input, Type expectedException)
{
    Assert.That(() => Subnet.Parse(input), Throws.InstanceOf(expectedException));
}

static IEnumerable<TestCaseData> Parse_InvalidCidr_ThrowsException_Test_Data()
{
    yield return new TestCaseData(null, typeof(ArgumentNullException))
        .SetName("Parse_NullCidr_ThrowsArgumentNullException_Test");
    yield return new TestCaseData("not-even-close", typeof(FormatException))
        .SetName("Parse_GarbageInput_ThrowsFormatException_Test");
    yield return new TestCaseData("256.0.0.0/24", typeof(FormatException))
        .SetName("Parse_InvalidOctet_ThrowsFormatException_Test");
}
```

## 10. Parallelizable Example

```csharp
[TestFixture]
[Parallelizable(ParallelScope.Children)]
public sealed class CpuBoundTests
{
    [Test]
    public void Hash_LargeInput_ReturnsCorrectDigest_Test() { /* ... */ }
    [Test]
    public void Compress_LargeInput_ReturnsSmallerOutput_Test() { /* ... */ }
    [Test]
    public void Encrypt_ValidKey_ReturnsCiphertext_Test() { /* ... */ }
}
```

With `[NonParallelizable]` for tests that must run alone:

```csharp
[Test]
[NonParallelizable]
public void SharedResource_Access_ExclusiveLock_Test() { /* ... */ }
```
