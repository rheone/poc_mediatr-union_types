# NSubstitute Examples

Setup shared across examples:

```csharp
public interface IRepository
{
    Task<User?> GetByIdAsync(int id);
    Task SaveAsync(User user);
    IEnumerable<string> Search(string query);
}

public record User(int Id, string Name);

public class UserService(IRepository repo)
{
    public Task<User?> GetUserAsync(int id) => repo.GetByIdAsync(id);
    public Task CreateUserAsync(int id, string name) => repo.SaveAsync(new(id, name));
    public List<string> Find(string query) => repo.Search(query).ToList();
}
```

## 1. Creating and Injecting a Substitute

```csharp
var repo = Substitute.For<IRepository>();
var sut = new UserService(repo);
repo.GetByIdAsync(1).Returns(new User(1, "Alice"));

var result = await sut.GetUserAsync(1);

Assert.That(result!.Name, Is.EqualTo("Alice"));
```

## 2. ReturnsAsync for Async Methods

```csharp
var repo = Substitute.For<IRepository>();
repo.GetByIdAsync(42).ReturnsAsync(new User(42, "Bob"));

var result = await repo.GetByIdAsync(42);

Assert.That(result!.Name, Is.EqualTo("Bob"));
```

## 3. ReturnsNull for Nullable Returns

```csharp
var repo = Substitute.For<IRepository>();
repo.GetByIdAsync(Arg.Any<int>()).ReturnsNull();

var result = await repo.GetByIdAsync(999);

Assert.That(result, Is.Null);
```

## 4. Arg.Is Predicate Matching

```csharp
var repo = Substitute.For<IRepository>();
var sut = new UserService(repo);

await sut.CreateUserAsync(1, "Alice");

await repo.Received(1).SaveAsync(Arg.Is<User>(u => u.Id == 1 && u.Name == "Alice"));
```

## 5. Received(1) and DidNotReceive Verification

```csharp
var repo = Substitute.For<IRepository>();
var sut = new UserService(repo);

sut.Find("test");

repo.Received(1).Search("test");
repo.DidNotReceive().Search("other");
```

## 6. Callback with Returns(ci => ...)

```csharp
var log = new List<string>();
var repo = Substitute.For<IRepository>();
repo.Search(Arg.Any<string>()).Returns(ci =>
{
    var q = ci.Arg<string>();
    log.Add($"search: {q}");
    return new[] { "a", "b" };
});
var sut = new UserService(repo);

var results = sut.Find("hello");

Assert.That(results, Has.Count.EqualTo(2));
Assert.That(log, Does.Contain("search: hello"));
```

## 7. When...DoNotCallBase (Partial Substitute)

```csharp
public class ServiceBase
{
    public virtual string GetData() => "real";
}

var partial = Substitute.ForPartsOf<ServiceBase>();
partial.When(x => x.GetData()).DoNotCallBase();

Assert.That(partial.GetData(), Is.Null);
```

## 8. ForPartsOf Testing Virtual Methods

```csharp
public class Calculator
{
    public virtual int Add(int a, int b) => a + b;
    public int DoubleAdd(int a, int b) => Add(a, b) * 2;
}

var calc = Substitute.ForPartsOf<Calculator>();
calc.Add(2, 3).Returns(10);

var result = calc.DoubleAdd(2, 3);

Assert.That(result, Is.EqualTo(20));
```

## 9. Argument Capture with Arg.Do

```csharp
var repo = Substitute.For<IRepository>();
User? captured = null;
repo.SaveAsync(Arg.Do<User>(u => captured = u));
var sut = new UserService(repo);

await sut.CreateUserAsync(2, "Bob");

Assert.That(captured!.Id, Is.EqualTo(2));
Assert.That(captured.Name, Is.EqualTo("Bob"));
```

## 10. Multiple Interface Substitute

```csharp
public interface ILogger { void Log(string msg); }
public interface IConfig { string Get(string key); }

var mock = Substitute.For<ILogger, IConfig>();

((ILogger)mock).Log("start");
((IConfig)mock).Get("mode").Returns("dark");

((ILogger)mock).Received(1).Log("start");
Assert.That(((IConfig)mock).Get("mode"), Is.EqualTo("dark"));
```

## 11. Auto-Property Stubbing

```csharp
public interface ISettings
{
    string ConnectionString { get; set; }
}

var settings = Substitute.For<ISettings>();
settings.ConnectionString = "Server=local;";

Assert.That(settings.ConnectionString, Is.EqualTo("Server=local;"));
```
