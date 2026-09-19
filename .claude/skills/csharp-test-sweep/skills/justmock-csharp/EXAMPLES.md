# JustMock Examples

All examples below work in **Free mode** unless explicitly marked `[Elevated]`.

## Interface Dependency — Returns

```csharp
public interface IUserRepository
{
    User GetById(int id);
}

[TestMethod]
public void GetUser_ExistingId_ReturnsUser()
{
    // Arrange
    var repo = Mock.Create<IUserRepository>();
    Mock.Arrange(() => repo.GetById(1)).Returns(new User { Id = 1, Name = "Alice" });

    var service = new UserService(repo);

    // Act
    var result = service.GetUser(1);

    // Assert
    Assert.AreEqual("Alice", result.Name);
}
```

## Arrange — Throws

```csharp
[TestMethod]
public void GetUser_MissingId_ThrowsNotFound()
{
    var repo = Mock.Create<IUserRepository>();
    Mock.Arrange(() => repo.GetById(It.IsAny<int>()))
        .Throws<NotFoundException>();

    var service = new UserService(repo);

    Assert.ThrowsException<NotFoundException>(() => service.GetUser(99));
}
```

## Verify — Occurs.Once

```csharp
[TestMethod]
public void SaveUser_CallsRepository_Once()
{
    var repo = Mock.Create<IUserRepository>();
    Mock.Arrange(() => repo.Save(Arg.IsAny<User>()))
        .DoInstead(() => { /* side effect only — arrange returns void */ });

    var service = new UserService(repo);
    service.CreateUser("Bob");

    Mock.Assert(() => repo.Save(Arg.IsAny<User>()), Occurs.Once());
}
```

## Verify — Occurs.Never

```csharp
[TestMethod]
public void GetUser_ReadOnly_DoesNotSave()
{
    var repo = Mock.Create<IUserRepository>();
    var service = new UserService(repo);

    service.GetUser(1);

    Mock.Assert(() => repo.Save(Arg.IsAny<User>()), Occurs.Never());
}
```

## Arg.Matches

```csharp
[TestMethod]
public void Process_ValidRequest_CallsSaveWithActiveFlag()
{
    var repo = Mock.Create<IUserRepository>();
    var service = new UserService(repo);

    service.Process(new Request
    {
        Id = 1,
        Name = "Complete",
        IsActive = true
    });

    Mock.Assert(() => repo.Save(Arg.Matches<Request>(
        r => r.IsActive && r.Name == "Complete")), Occurs.Once());
}
```

## Behavior.Strict

```csharp
[TestMethod]
public void StrictMode_UnarrangedCall_Throws()
{
    var mock = Mock.Create<IUserRepository>(Behavior.Strict);
    Mock.Arrange(() => mock.GetById(It.IsAny<int>())).Returns(new User());

    // This works — GetById is arranged
    mock.GetById(1);

    // This throws — Save was never arranged
    Assert.ThrowsException<MockException>(() => mock.Save(new User()));
}
```

## Mock.CreateLike

```csharp
public interface IConfiguration
{
    string Server { get; }
    int Port { get; }
    bool Enabled { get; }
    IList<string> Tags { get; }
}

[TestMethod]
public void DefaultConfig_AllPropertiesAreDefault()
{
    var config = Mock.CreateLike<IConfiguration>();

    Assert.AreEqual("", config.Server);    // string defaults to ""
    Assert.AreEqual(0, config.Port);       // int defaults to 0
    Assert.AreEqual(false, config.Enabled); // bool defaults to false
    Assert.IsNull(config.Tags);            // ref type defaults to null
}
```

## Callback

```csharp
[TestMethod]
public void SendEmail_CapturesRecipient()
{
    var sender = Mock.Create<IEmailSender>();
    string captured = null;
    Mock.Arrange(() => sender.Send(Arg.IsAny<string>()))
        .DoInstead((string to) => captured = to);

    var service = new NotificationService(sender);
    service.NotifyUser("bob@example.com", "Hello!");

    Assert.AreEqual("bob@example.com", captured);
}
```

## AssertMultiple

```csharp
[TestMethod]
public void CreateUser_SavesAndLogs()
{
    var repo = Mock.Create<IUserRepository>();
    var logger = Mock.Create<ILogger>();
    var service = new UserService(repo, logger);

    var user = service.CreateUser("Charlie");

    Mock.AssertMultiple(() =>
    {
        Mock.Assert(() => repo.Save(user), Occurs.Once());
        Mock.Assert(() => logger.Info("User created"), Occurs.Once());
    });
}
```

---

## Elevated Mode Examples

The following examples require a commercial Telerik JustMock license and the JustMock profiler. They **compile in Free mode but throw at runtime**.

### Mock Sealed Class [Elevated]

```csharp
[TestMethod]
public void SealedDependency_ReturnsValue()
{
    // JustMock profiler intercepts non-virtual members on sealed class
    var cache = Mock.Create<SealedCache>();
    Mock.Arrange(() => cache.Get("key")).Returns("cached-value");

    var service = new ReportingService(cache);
    var result = service.GetReport("key");

    Assert.AreEqual("cached-value", result);
}
```

### Mock Static Method [Elevated]

```csharp
[TestMethod]
public void StaticMethod_ReturnsFixedValue()
{
    Mock.Arrange(() => File.ReadAllText(Arg.IsAny<string>()))
        .Returns("{\"name\": \"mocked\"}");

    var result = JsonConfigLoader.Load("config.json");

    Assert.AreEqual("mocked", result.Name);
}
```

### Intercept DateTime.Now [Elevated]

```csharp
[TestMethod]
public void Timestamp_ReturnsFixedDate()
{
    var fixedDate = new DateTime(2025, 6, 1, 12, 0, 0);
    Mock.Arrange(() => DateTime.Now).Returns(fixedDate);

    var service = new OrderService();
    var result = service.CreateOrder();

    Assert.AreEqual(fixedDate, result.CreatedAt);
}
```
