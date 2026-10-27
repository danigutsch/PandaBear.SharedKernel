using PandaBear.Results;
using PandaBear.Testing;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests creation and validation of the <c>Idempotency-Key</c> header value object.</summary>
[Trait(SharedKernelTestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class IdempotencyKeyHeaderTests
{
    [Theory]
    [InlineData(null, "Idempotency-Key is required.", "Idempotency-Key is required.")]
    [InlineData("", "Idempotency-Key must use the supported opaque token format.", "Idempotency-Key is invalid.")]
    [InlineData(" ", "Idempotency-Key must use the supported opaque token format.", "Idempotency-Key is invalid.")]
    [InlineData("invalid key", "Idempotency-Key must use the supported opaque token format.", "Idempotency-Key is invalid.")]
    [InlineData("first-key,second-key", "Idempotency-Key must use the supported opaque token format.", "Idempotency-Key is invalid.")]
    [InlineData("é", "Idempotency-Key must use the supported opaque token format.", "Idempotency-Key is invalid.")]
    public void Invalid_headers_return_errors_for_the_header_field(string? value, string detail, string message)
    {
        // Act
        var result = IdempotencyKeyHeader.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe(detail);
        var errors = error.ValidationErrors.ShouldNotBeNull();
        errors.Count.ShouldBe(1);
        errors[IdempotencyKeyHeader.Name].ShouldHaveSingleItem().ShouldBe(message);
    }

    [Theory]
    [InlineData("key-123", "key-123")]
    [InlineData(" key-123 ", "key-123")]
    [InlineData("A._:-09", "A._:-09")]
    public void Valid_headers_create_normalized_value_objects(string value, string expected)
    {
        // Act
        var result = IdempotencyKeyHeader.Create(value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Header_length_uses_the_value_object_limit(int length, bool valid)
    {
        // Arrange
        var value = new string('x', length);

        // Act
        var result = IdempotencyKeyHeader.Create(value);

        // Assert
        result.IsSuccess.ShouldBe(valid);
        if (valid)
        {
            result.Value.Value.ShouldBe(value);
        }
        else
        {
            result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(
                "Idempotency-Key must use the supported opaque token format.");
        }
    }
}
