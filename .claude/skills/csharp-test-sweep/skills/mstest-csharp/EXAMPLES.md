# MSTest Examples

## Basic Test Class

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Add_TwoPositiveNumbers_ReturnsSum_Test()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        var result = calc.Add(2, 3);

        // Assert
        Assert.AreEqual(5, result);
    }
}
```

## Parameterized with DataRow

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(-1, 1, 0)]
    [DataRow(0, 0, 0)]
    public void Add_ValidInputs_ReturnsExpected_Test(int a, int b, int expected)
    {
        var calc = new Calculator();
        var result = calc.Add(a, b);
        Assert.AreEqual(expected, result);
    }
}
```

## DynamicData Source

```csharp
private static IEnumerable<object[]> AdditionData =>
    new[]
    {
        [1, 2, 3],
        [-1, 1, 0],
        [int.MaxValue, 0, int.MaxValue],
    };

[TestMethod]
[DynamicData(nameof(AdditionData), DynamicDataSourceType.Property)]
public void Add_MultipleCases_ReturnsExpected_Test(int a, int b, int expected)
{
    var calc = new Calculator();
    var result = calc.Add(a, b);
    Assert.AreEqual(expected, result);
}
```

## Assert.Multiple

```csharp
[TestMethod]
public void CreateUser_SetsAllProperties_Test()
{
    var user = new UserService().Create("alice", "alice@example.com");

    Assert.Multiple(() =>
    {
        Assert.IsNotNull(user);
        Assert.AreEqual("alice", user.Username);
        Assert.AreEqual("alice@example.com", user.Email);
        Assert.IsFalse(user.IsAdmin);
    });
}
```

## StringAssert and CollectionAssert

```csharp
[TestMethod]
public void GetLogs_FilterByLevel_ReturnsMatchingEntries_Test()
{
    var logs = new Logger().GetEntries("ERROR");

    Assert.Multiple(() =>
    {
        StringAssert.Contains(logs[0].Message, "Failed");
        StringAssert.Matches(logs[0].Timestamp, new Regex(@"^\d{4}-\d{2}-\d{2}"));
        CollectionAssert.AllItemsAreUnique(logs.Select(l => l.Id).ToList());
        CollectionAssert.IsSubsetOf(
            new[] { "ERROR" },
            logs.Select(l => l.Level).Distinct().ToList());
    });
}
```

## Test Lifecycle

```csharp
[TestClass]
public class DatabaseTests
{
    private static Database _db;

    [ClassInitialize]
    public static void InitClass(TestContext ctx)
    {
        _db = new Database();
        _db.Connect("test-connection-string");
    }

    [TestInitialize]
    public void Init()
    {
        _db.BeginTransaction();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Rollback();
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        _db.Disconnect();
    }

    [TestMethod]
    public void SaveUser_PersistsToDatabase_Test()
    {
        _db.Save(new User("bob"));
        Assert.IsNotNull(_db.Find("bob"));
    }
}
```

## Async Test

```csharp
[TestMethod]
public async Task GetUser_InvalidId_ThrowsNotFound_Test()
{
    var service = new UserService();

    await Assert.ThrowsExceptionAsync<NotFoundException>(
        async () => await service.GetUserAsync(-1));
}
```

## Metadata

```csharp
[TestClass]
[TestCategory("Integration")]
[Owner("jane")]
public class PaymentGatewayTests
{
    [TestMethod]
    [WorkItem(12345)]
    [GitHubWorkItem(42)]
    [Description("Validates end-to-end credit card charge flow")]
    [Priority(1)]
    public void ChargeCard_ValidAmount_ReturnsReceipt_Test()
    {
        // ...
    }
}
```

## DiscoverInternals

```csharp
// In AssemblyInfo.cs or any file in the test project:
[assembly: DiscoverInternals]

// Internal methods are now discovered by MSTest:
[TestClass]
internal class InternalParserTests
{
    [TestMethod]
    internal void Parse_ValidJson_ReturnsObject_Test()
    {
        // ...
    }
}
```
