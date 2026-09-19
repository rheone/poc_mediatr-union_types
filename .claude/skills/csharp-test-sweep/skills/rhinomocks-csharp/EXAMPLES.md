# RhinoMocks Examples

All examples use xUnit; the RhinoMocks API is identical across test frameworks.

## 1. AAA Style — Create, Stub, AssertWasCalled

```csharp
public interface ICalculator
{
    int Add(int a, int b);
}

public class CalculatorClient
{
    private readonly ICalculator _calc;
    public CalculatorClient(ICalculator calc) => _calc = calc;
    public int DoubleSum(int a, int b) => _calc.Add(a, b) * 2;
}

[Fact]
public void DoubleSum_delegates_to_calculator_and_doubles_result()
{
    var mock = MockRepository.GenerateMock<ICalculator>();
    mock.Stub(x => x.Add(2, 3)).Return(5);

    var sut = new CalculatorClient(mock);
    var result = sut.DoubleSum(2, 3);

    Assert.Equal(10, result);
    mock.AssertWasCalled(x => x.Add(2, 3));
}
```

## 2. Argument Matcher — Is.Equal and Is.Anything

```csharp
[Fact]
public void Logger_is_called_with_message()
{
    var mock = MockRepository.GenerateMock<ILogger>();
    var sut = new Service(mock);

    sut.Process("input");

    mock.AssertWasCalled(x => x.Log(Arg<string>.Is.Anything));
    mock.AssertWasCalled(x => x.Log(Arg<string>.Is.Equal("Processing: input")));
}
```

## 3. GenerateStub for Property Behavior

```csharp
public interface IConfig
{
    string ConnectionString { get; set; }
    int TimeoutSeconds { get; set; }
}

[Fact]
public void Repository_uses_connection_string_from_config()
{
    var config = MockRepository.GenerateStub<IConfig>();
    config.ConnectionString = "Server=localhost;Database=test";
    config.TimeoutSeconds = 30;

    var sut = new Repository(config);
    var result = sut.Connect();

    Assert.True(result);
}
```

## 4. StructureMap.AutoMocking / RhinoAutoMocker

```csharp
public class OrderService
{
    public OrderService(IOrderRepository repo, IEmailSender email, ILogger logger) { ... }
    public void PlaceOrder(Order order) { ... }
}

[Fact]
public void PlaceOrder_sends_email_and_logs()
{
    var autoMocker = new RhinoAutoMocker<OrderService>(MockMode.AAA);
    var order = new Order { Id = 1 };

    autoMocker.ClassUnderTest.PlaceOrder(order);

    autoMocker.Get<IEmailSender>()
        .AssertWasCalled(x => x.Send(Arg<string>.Is.Anything));
    autoMocker.Get<ILogger>()
        .AssertWasCalled(x => x.Log(Arg<string>.Is.Anything));
}
```

## 5. Partial Mock with GeneratePartialMock

```csharp
public class DataProcessor
{
    public virtual string Transform(string input) => input.ToUpper();
    public string Process(string input) => Transform(input) + "!";
}

[Fact]
public void Process_appends_exclamation_mark()
{
    var mock = MockRepository.GeneratePartialMock<DataProcessor>();
    mock.Stub(x => x.Transform("hello")).Return("CUSTOM");

    var result = mock.Process("hello");

    Assert.Equal("CUSTOM!", result);
}
```

## 6. Record/Replay Example (Deprecated)

```csharp
[Fact]
public void Record_replay_style_deprecated()
{
    var mock = MockRepository.GenerateMock<ICalculator>();
    mock.Expect(x => x.Add(1, 1)).Return(3);
    // no AAA—record/replay switch needed
    var repository = MockRepository.FromInstance(mock);

    repository.ReplayAll();

    var result = mock.Add(1, 1);

    Assert.Equal(3, result);
    repository.VerifyAll();
}
```

## 7. Multiple Return Values with Repeat

```csharp
public interface IQueue
{
    int Dequeue();
}

[Fact]
public void Multiple_dequeue_returns_different_values()
{
    var mock = MockRepository.GenerateMock<IQueue>();
    mock.Stub(x => x.Dequeue())
        .Return(10).Repeat.Once()
        .Return(20).Repeat.Once()
        .Return(30).Repeat.Once();

    Assert.Equal(10, mock.Dequeue());
    Assert.Equal(20, mock.Dequeue());
    Assert.Equal(30, mock.Dequeue());
}
```

## 8. Concrete Subclass for Testing Abstract Classes

```csharp
public abstract class AbstractProcessor
{
    public virtual string Transform(string input) => input.ToUpper();
    public string Process(string input) => Transform(input) + "!";
}

// Testable subclass — does NOT override the method under test
private class TestableProcessor : AbstractProcessor { }

[Fact]
public void Process_transforms_and_appends()
{
    var sut = new TestableProcessor();

    var result = sut.Process("hello");

    Assert.Equal("HELLO!", result);
}
```
