# Reference: Fixture Patterns (xUnit v3)

## Decision Table

| Situation | Use |
| --------- | --- |
| Setup is cheap and pure (no I/O, no shared state) | Constructor / `IDisposable.Dispose` |
| Setup is expensive but safe to share across tests **in one class** | `IClassFixture<T>` |
| Setup must be shared across **multiple test classes** | `ICollectionFixture<T>` + `[Collection]` |
| Each test must have a clean, isolated state regardless of cost | Constructor / `IDisposable.Dispose` always |

---

## Pattern 1 — Constructor / `IDisposable` (most common)

Use when setup is cheap or when each test must start clean.

```csharp
public sealed class SubnetTests : IDisposable
{
    private readonly Subnet _subnet;

    public SubnetTests()
    {
        // Arrange shared per-test state here
        _subnet = Subnet.Parse("192.168.0.0/24");
    }

    public void Dispose()
    {
        // Clean up per-test resources (e.g. temp files, streams)
    }

    [Fact]
    public void Head_ReturnsFirstAddress_Test()
    {
        // Arrange (additional, test-specific)
        var expected = IPAddress.Parse("192.168.0.0");

        // Act
        var actual = _subnet.Head;

        // Assert
        Assert.Equal(expected, actual);
    }
}
```

For async teardown, implement `IAsyncLifetime` instead:

```csharp
public sealed class SubnetTests : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        // async setup
    }

    public async ValueTask DisposeAsync()
    {
        // async teardown
    }
}
```

---

## Pattern 2 — `IClassFixture<T>` (shared within one class)

Use when setup is expensive (e.g. parsing a large file, starting an in-process server) and the shared state is safe to reuse across all tests in the class.

```csharp
// The fixture — created once per test class
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

// The test class
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

**Important:** The fixture is shared — mutations in one test affect subsequent tests. Either make tests read-only or reset state explicitly between tests.

---

## Pattern 3 — `ICollectionFixture<T>` + `[Collection]` (shared across classes)

Use when multiple test classes need the same expensive shared resource (e.g. a Docker container, a shared HTTP server).

```csharp
// Step 1 — define the fixture
public sealed class ApiServerFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = default!;
    private TestServer _server = default!;

    public async ValueTask InitializeAsync()
    {
        _server = await TestServer.StartAsync();
        Client = _server.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _server.StopAsync();
    }
}

// Step 2 — define the collection (in its own file or alongside the fixture)
[CollectionDefinition("ApiServer")]
public sealed class ApiServerCollection : ICollectionFixture<ApiServerFixture>
{
    // No members needed — this class is a marker only
}

// Step 3 — apply the collection to every class that shares the fixture
[Collection("ApiServer")]
public sealed class ProductsApiTests
{
    private readonly HttpClient _client;

    public ProductsApiTests(ApiServerFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetProducts_ReturnsOk_Test()
    {
        var response = await _client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

[Collection("ApiServer")]
public sealed class OrdersApiTests
{
    private readonly HttpClient _client;

    public OrdersApiTests(ApiServerFixture fixture)
    {
        _client = fixture.Client;
    }

    // ...
}
```

**Rules:**
- All classes in the same `[Collection("name")]` share one fixture instance and run **sequentially** — they will not execute in parallel with each other.
- Classes **not** in any collection run in parallel with all other non-collection classes.
- The collection name string must match exactly between `[CollectionDefinition]` and `[Collection]`.

---

## What NOT to Do

```csharp
// BAD — disables all parallelism globally, silences flakiness without fixing it
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// BAD — static mutable state shared across tests
public static class SharedState
{
    public static int Counter = 0; // tests will interfere with each other
}
```
