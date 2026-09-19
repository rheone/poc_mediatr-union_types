# Moq 4.x Examples

**Interface under test throughout:**
```csharp
public interface IUserRepository
{
    User? Find(int id);
    Task<User?> FindAsync(int id);
    void Save(User user);
    string? GetStatus(int id);
}
```

## 1. Creating and Injecting a Mock Dependency

```csharp
[Fact]
public void Find_ExistingUser_ReturnsUser()
{
    var mock = new Mock<IUserRepository>();
    mock.Setup(r => r.Find(1)).Returns(new User { Id = 1, Name = "Alice" });

    var service = new UserService(mock.Object);
    var user = service.Lookup(1);

    Assert.Equal("Alice", user.Name);
}
```

## 2. Returns, ReturnsAsync, Throws

```csharp
mock.Setup(r => r.Find(99)).Returns((User?)null);
mock.Setup(r => r.FindAsync(1)).ReturnsAsync(new User { Id = 1 });

mock.Setup(r => r.Save(It.IsAny<User>()))
    .Throws<InvalidOperationException>();
```

## 3. It.IsAny and It.Is Predicate Matching

```csharp
// Match any int
mock.Setup(r => r.Find(It.IsAny<int>())).Returns(new User { Id = 0 });

// Match positive Ids only
mock.Setup(r => r.Find(It.Is<int>(id => id > 0)))
    .Returns((int id) => new User { Id = id });
```

## 4. Verify with Times.Once and Times.Never

```csharp
mock.Object.Save(new User { Id = 1 });

mock.Verify(r => r.Save(It.IsAny<User>()), Times.Once());
mock.Verify(r => r.Find(It.IsAny<int>()), Times.Never());
```

## 5. Callback for Capturing Arguments

```csharp
var saved = new List<User>();
mock.Setup(r => r.Save(It.IsAny<User>()))
    .Callback<User>(u => saved.Add(u));

mock.Object.Save(new User { Id = 1 });
mock.Object.Save(new User { Id = 2 });

Assert.Equal(2, saved.Count);
Assert.Equal(1, saved[0].Id);
```

## 6. SetupSequence for Ordered Returns

```csharp
mock.SetupSequence(r => r.GetStatus(1))
    .Returns("Pending")
    .Returns("Approved")
    .Returns("Completed");

Assert.Equal("Pending",   mock.Object.GetStatus(1));
Assert.Equal("Approved",  mock.Object.GetStatus(1));
Assert.Equal("Completed", mock.Object.GetStatus(1));
Assert.Equal("Completed", mock.Object.GetStatus(1)); // last value repeats
```

## 7. Mock.Of<T> with LINQ Expressions

```csharp
var repo = Mock.Of<IUserRepository>(r =>
    r.Find(1) == new User { Id = 1, Name = "Alice" } &&
    r.Find(2) == new User { Id = 2, Name = "Bob" });

var service = new UserService(repo);
Assert.Equal("Alice", service.Lookup(1).Name);
```

## 8. Strict vs Loose Behavior

```csharp
// Loose — unsetup calls return defaults, no exception
var loose = new Mock<IUserRepository>(MockBehavior.Loose);
Assert.Null(loose.Object.Find(42));  // no exception, returns null

// Strict — unsetup calls throw
var strict = new Mock<IUserRepository>(MockBehavior.Strict);
Assert.Throws<MockException>(() => strict.Object.Find(42));
```

## 9. Verifiable + VerifyAll

```csharp
var mock = new Mock<IUserRepository>();
mock.Setup(r => r.Save(It.IsAny<User>())).Verifiable();
mock.Setup(r => r.Find(1)).Returns(new User()).Verifiable();

mock.Object.Save(new User());

var ex = Assert.Throws<MockException>(() => mock.VerifyAll());
Assert.Contains("Find", ex.Message); // Find was set up but never called
```

## 10. Property Auto-Stubbing

```csharp
public interface IConfig
{
    string ConnectionString { get; set; }
    int TimeoutSeconds { get; set; }
}

var mock = new Mock<IConfig>();
mock.SetupProperty(c => c.ConnectionString, "default");
mock.SetupProperty(c => c.TimeoutSeconds, 30);

Assert.Equal("default", mock.Object.ConnectionString);

mock.Object.ConnectionString = "updated";
mock.Object.TimeoutSeconds = 60;

Assert.Equal("updated", mock.Object.ConnectionString);
Assert.Equal(60, mock.Object.TimeoutSeconds);
```
