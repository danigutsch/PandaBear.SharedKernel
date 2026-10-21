using System.Text.Json;

namespace PandaBear.Idempotency.Tests;

public sealed class TypedRequestFingerprintTests
{
    [Theory]
    [InlineData("{\"b\":2.00,\"a\":1.0}", "{\"a\":1,\"b\":2}")]
    [InlineData("{\"outer\":{\"b\":2,\"a\":1.00}}", "{\"outer\":{\"a\":1,\"b\":2.0}}")]
    [InlineData("[1.0,2.00,null,true,\"text\"]", "[1,2,null,true,\"text\"]")]
    public void Object_property_order_and_decimal_scale_do_not_change_request_identity(string firstJson, string secondJson)
    {
        // Arrange
        using var first = JsonDocument.Parse(firstJson);
        using var second = JsonDocument.Parse(secondJson);

        // Act
        var left = IdempotencyRequestFingerprint.CreateJson(first.RootElement, FingerprintJsonContext.Default.JsonElement, "actor", "resource");
        var right = IdempotencyRequestFingerprint.CreateJson(second.RootElement, FingerprintJsonContext.Default.JsonElement, "actor", "resource");

        // Assert
        left.ShouldBe(right);
    }

    [Fact]
    public void Typed_payloads_preserve_the_existing_context_and_length_framed_hash_contract()
    {
        // Arrange
        using var input = JsonDocument.Parse("{\"amount\":100.00}");
        var expected = IdempotencyRequestFingerprint.Create("booking", "target", "actor", "{\"amount\":100}");

        // Act
        var actual = IdempotencyRequestFingerprint.CreateJson(input.RootElement, FingerprintJsonContext.Default.JsonElement, "booking", "target", "actor");
        var otherActor = IdempotencyRequestFingerprint.CreateJson(input.RootElement, FingerprintJsonContext.Default.JsonElement, "booking", "target", "other");

        // Assert
        actual.ShouldBe(expected);
        actual.ShouldNotBe(otherActor);
    }

    [Theory]
    [InlineData("[1,2]", "[2,1]")]
    [InlineData("{\"value\":\" a\"}", "{\"value\":\"a\"}")]
    [InlineData("{\"amount\":1}", "{\"amount\":2}")]
    public void Array_order_strings_and_changed_values_remain_significant(string firstJson, string secondJson)
    {
        // Arrange
        using var first = JsonDocument.Parse(firstJson);
        using var second = JsonDocument.Parse(secondJson);

        // Act
        var left = IdempotencyRequestFingerprint.CreateJson(first.RootElement, FingerprintJsonContext.Default.JsonElement);
        var right = IdempotencyRequestFingerprint.CreateJson(second.RootElement, FingerprintJsonContext.Default.JsonElement);

        // Assert
        left.ShouldNotBe(right);
    }
}
