using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;

using TestContext = Xunit.TestContext;
namespace SharedKernel.Blazor.Tests;

/// <summary>Tests retaining edited text while participating in Blazor form validation.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ParsedTextInputStateTests
{
    [Fact]
    public async Task Invalid_input_stays_visible_and_commits_only_when_it_parses()
    {
        // Arrange
        var context = new EditContext(new object());
        var field = context.Field("position");
        using var input = new ParsedTextInputState<int>(
            value => value.ToString(CultureInfo.InvariantCulture),
            text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)
                ? (whole, null) : (default, "Enter a whole number."));
        input.Bind(context, field, 12);

        // Act
        var invalidEdit = input.TrySetText("1.2", out _);
        input.Bind(context, field, 12);
        var rejectedText = input.Text;
        var invalidForm = await context.ValidateAsync(TestContext.Current.CancellationToken);
        var errors = context.GetValidationMessages(field).ToArray();
        var validEdit = input.TrySetText(" 017 ", out var committed);
        input.Bind(context, field, committed);
        var validForm = await context.ValidateAsync(TestContext.Current.CancellationToken);

        // Assert
        invalidEdit.ShouldBeFalse();
        rejectedText.ShouldBe("1.2");
        invalidForm.ShouldBeFalse();
        errors.ShouldBe(["Enter a whole number."]);
        validEdit.ShouldBeTrue();
        committed.ShouldBe(17);
        input.Text.ShouldBe(" 017 ");
        validForm.ShouldBeTrue();
        context.GetValidationMessages(field).ShouldBeEmpty();
    }

    [Fact]
    public async Task Rebinding_a_field_moves_its_message_and_disposal_clears_it()
    {
        // Arrange
        var context = new EditContext(new object());
        var first = context.Field("sections[1].size");
        var second = context.Field("sections[0].size");
        using var input = new ParsedTextInputState<decimal>(
            value => value.ToString(CultureInfo.InvariantCulture),
            text => decimal.TryParse(text, CultureInfo.InvariantCulture, out var size)
                ? (size, null) : (default, "Enter a size."));
        input.Bind(context, first, 2.5m);
        input.TrySetText("missing", out _);
        _ = await context.ValidateAsync(TestContext.Current.CancellationToken);

        // Act
        input.Bind(context, second, 2.5m);
        var retainedText = input.Text;
        var firstErrors = context.GetValidationMessages(first).ToArray();
        var secondErrors = context.GetValidationMessages(second).ToArray();
        input.Dispose();
        var afterDisposal = await context.ValidateAsync(TestContext.Current.CancellationToken);

        // Assert
        retainedText.ShouldBe("missing");
        firstErrors.ShouldBeEmpty();
        secondErrors.ShouldBe(["Enter a size."]);
        afterDisposal.ShouldBeTrue();
    }
}
