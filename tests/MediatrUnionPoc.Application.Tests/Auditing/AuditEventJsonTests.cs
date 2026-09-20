using System.Text.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Application.Tests.Auditing;

/// <summary>
/// Tests <see cref="AuditEventJson"/>: the camelCase wire shape, that null members are omitted, and
/// that free text can never break out of its line and forge a second record.
/// </summary>
public sealed class AuditEventJsonTests
{
    /// <summary>Verifies every member is written under its camelCase name with the expected values.</summary>
    [Fact]
    public void Serialize_FullEvent_WritesCamelCaseMembers_Test()
    {
        // Arrange
        var id = Guid.NewGuid();
        var auditEvent = Event() with
        {
            Id = id,
            ActorId = "root",
            EffectiveId = "alice",
            IsImpersonated = true,
            TokenId = "jti-1",
            TargetType = "User",
            TargetId = "alice",
            Reason = "why",
            Ticket = "SUP-1",
            TraceId = "abc",
            SourceIp = "203.0.113.7",
            Details = new Dictionary<string, string> { ["roles"] = "Support" },
        };

        // Act
        using var json = JsonDocument.Parse(AuditEventJson.Serialize(auditEvent));

        // Assert
        var root = json.RootElement;
        Assert.Multiple(
            () => Assert.Equal(id, root.GetProperty("id").GetGuid()),
            () =>
                Assert.Equal(
                    "2026-03-04T05:06:07+00:00",
                    root.GetProperty("timestamp").GetString()
                ),
            () => Assert.Equal("Impersonation.IssueToken", root.GetProperty("action").GetString()),
            () => Assert.Equal("ImpersonationToken", root.GetProperty("outcome").GetString()),
            () => Assert.Equal("root", root.GetProperty("actorId").GetString()),
            () => Assert.Equal("alice", root.GetProperty("effectiveId").GetString()),
            () => Assert.True(root.GetProperty("isImpersonated").GetBoolean()),
            () => Assert.Equal("jti-1", root.GetProperty("tokenId").GetString()),
            () => Assert.Equal("User", root.GetProperty("targetType").GetString()),
            () => Assert.Equal("alice", root.GetProperty("targetId").GetString()),
            () => Assert.Equal("why", root.GetProperty("reason").GetString()),
            () => Assert.Equal("SUP-1", root.GetProperty("ticket").GetString()),
            () => Assert.Equal("abc", root.GetProperty("traceId").GetString()),
            () => Assert.Equal("203.0.113.7", root.GetProperty("sourceIp").GetString()),
            () =>
                Assert.Equal(
                    "Support",
                    root.GetProperty("details").GetProperty("roles").GetString()
                )
        );
    }

    /// <summary>Verifies null members are left out rather than written as nulls.</summary>
    [Fact]
    public void Serialize_MinimalEvent_OmitsNullMembers_Test()
    {
        // Act
        var json = JsonNode.Parse(AuditEventJson.Serialize(Event()))!.AsObject();

        // Assert
        var names = json.Select(pair => pair.Key).Order().ToArray();
        Assert.Equal(["action", "details", "id", "isImpersonated", "outcome", "timestamp"], names);
    }

    /// <summary>Verifies line breaks, quotes and a pasted fake record inside free text stay inside one line of JSON.</summary>
    [Fact]
    public void Serialize_ReasonWithNewlinesAndQuotes_StaysOneLine_Test()
    {
        // Arrange
        var forged =
            "line one\r\n{\"action\":\"Product.Delete\",\"outcome\":\"ProductDto\"}\nline \"three\""
            + (char)0x2028
            + (char)0x85;
        var auditEvent = Event() with
        {
            Reason = forged,
            Ticket = forged,
            TargetId = forged,
            Details = new Dictionary<string, string> { ["denial"] = forged },
        };

        // Act
        var line = AuditEventJson.Serialize(auditEvent);

        // Assert
        using var json = JsonDocument.Parse(line);
        Assert.Multiple(
            () => Assert.DoesNotContain('\n', line),
            () => Assert.DoesNotContain('\r', line),
            () => Assert.Equal(forged, json.RootElement.GetProperty("reason").GetString()),
            () =>
                Assert.Equal(
                    "Impersonation.IssueToken",
                    json.RootElement.GetProperty("action").GetString()
                )
        );
    }

    /// <summary>Verifies a null event is rejected.</summary>
    [Fact]
    public void Serialize_Null_ThrowsArgumentNullException_Test() =>
        Assert.Throws<ArgumentNullException>(() => AuditEventJson.Serialize(null!));

    private static AuditEvent Event() =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            Action = "Impersonation.IssueToken",
            Outcome = "ImpersonationToken",
        };
}
