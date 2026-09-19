# C# Test Sweep

Class-by-class quality pass over a C# test suite. Detects the test framework (xUnit, NUnit, MSTest) and mocking library (NSubstitute, Moq, RhinoMocks, JustMock) from the project files and applies the matching companion rules. The workflow is in [SKILL.md](SKILL.md).

```text
/csharp-test-sweep                          # full sweep
/csharp-test-sweep My.Namespace             # one namespace
/csharp-test-sweep ClassName                # one test class
/csharp-test-sweep ClassName#MemberName     # one #region
/csharp-test-sweep ClassName.Method_Test    # one test method
```
