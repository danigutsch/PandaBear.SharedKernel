using System.Net;
using System.Text;
using System.Xml;
using Microsoft.Playwright;
using PandaBear.Results;
using PandaBear.Testing.Http;
using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

public sealed class UserMessageOverloadTests
{
    private const string Reason = "Deleted orders stay deleted.";

    [Fact]
    public void Message_overload_states_the_reason_before_an_xUnit_failure()
    {
        // Arrange
        object? order = "order";
        Action assertion = () => order.ShouldBeNull(Reason);

        // Act
        var exception = assertion.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        var failure = exception.InnerException.ShouldBeOfType<Xunit.Sdk.NullException>();
        exception.Message.ShouldBe(Reason + Environment.NewLine + failure.Message);
    }

    [Fact]
    public void Message_overload_states_the_reason_before_a_failure_that_is_not_an_xUnit_assertion()
    {
        // Arrange
        const string unclosedDocument = "<!DOCTYPE html><html><head></head><body>";
        Action invocation = () => { };
        Action documentAssertion = () => unclosedDocument.ShouldBeWellFormedHtmlDocument(Reason);
        Action invocationAssertion = () => invocation.ShouldThrowInner<InvalidOperationException>(Reason);

        // Act
        var documentException = documentAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var invocationException = invocationAssertion.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        var documentFailure = documentException.InnerException.ShouldBeOfType<XmlException>();
        var invocationFailure = invocationException.InnerException.ShouldBeOfType<InvalidOperationException>();
        documentException.Message.ShouldBe(Reason + Environment.NewLine + documentFailure.Message);
        invocationException.Message.ShouldBe(Reason + Environment.NewLine + invocationFailure.Message);
    }

    [Fact]
    public void Message_overload_passes_argument_errors_through_unchanged()
    {
        // Arrange
        Action assertion = () => 1.0.ShouldBe(2.0, 99, Reason);

        // Act, Assert
        assertion.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Message_overload_passes_a_dynamic_skip_through_unchanged()
    {
        // Arrange
        Action skippedInvocation = () => throw Xunit.Sdk.SkipException.ForSkip("The browser is not installed.");

        // Act
        // xUnit exception assertions let a dynamic skip escape, which would skip this test, so a task captures the skip
        // and WhenAny waits for it without rethrowing.
        var invocation = Task.Run(() => skippedInvocation.ShouldThrowInner<InvalidOperationException>(Reason), TestContext.Current.CancellationToken);
        await Task.WhenAny(invocation);

        // Assert
        var exception = invocation.Exception.ShouldNotBeNull().InnerException.ShouldBeOfType<Xunit.Sdk.SkipException>();
        exception.Message.ShouldStartWith("$XunitDynamicSkip$", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Message_overload_rejects_a_null_user_message_even_when_the_assertion_holds()
    {
        // Arrange
        object? missingOrder = null;
        int[] orders = [42];
        Func<Task> rejectsOrder = () => Task.FromException(new InvalidOperationException());
        Action nullAssertion = () => missingOrder.ShouldBeNull(userMessage: null!);
        Action singleItemAssertion = () => orders.ShouldHaveSingleItem(userMessage: null!);
        Func<Task> asyncAssertion = () => rejectsOrder.ShouldThrowAssignableTo<InvalidOperationException>(userMessage: null!);

        // Act
        var nullException = nullAssertion.ShouldThrow<ArgumentNullException>();
        var singleItemException = singleItemAssertion.ShouldThrow<ArgumentNullException>();
        var asyncException = await asyncAssertion.ShouldThrow<ArgumentNullException>();

        // Assert
        nullException.ParamName.ShouldBe("userMessage");
        singleItemException.ParamName.ShouldBe("userMessage");
        asyncException.ParamName.ShouldBe("userMessage");
    }

    [Fact]
    public void Message_overload_returns_the_checked_value()
    {
        // Arrange
        int[] orders = [42];
        var result = Result.Ok("created");
        string? orderName = "order";
        int? orderNumber = 7;

        // Act
        var order = orders.ShouldHaveSingleItem(Reason);
        var value = result.ShouldBeSuccess(Reason);
        var name = orderName.ShouldNotBeNull(Reason);
        var number = orderNumber.ShouldNotBeNull(Reason);

        // Assert
        order.ShouldBe(42);
        value.ShouldBe("created");
        name.ShouldBeSameAs(orderName);
        number.ShouldBe(7);
    }

    [Fact]
    public void Message_overload_keeps_the_null_state_of_the_checked_value()
    {
        // Arrange
        string?[] orderNames = ["order"];
        int?[] orderNumbers = [7];
        var orderName = orderNames[0];
        var orderNumber = orderNumbers[0];

        // Act
        orderName.ShouldNotBeNull(Reason);
        orderNumber.ShouldNotBeNull(Reason);

        // Assert
        // These reads compile without nullable warnings only while the message overloads keep [NotNull].
        orderName.Length.ShouldBe(5);
        orderNumber.Value.ShouldBe(7);
    }

    [Fact]
    public void Ordinal_message_overloads_compare_ordinally()
    {
        // Arrange
        const string text = "abc";
        // Culture comparison treats a precomposed letter and its decomposed spelling as equal; ordinal comparison does not.
        const string precomposed = "\u00C5";
        const string decomposed = "A\u030A";
        Action containsAssertion = () => text.ShouldContain("B", userMessage: Reason);
        Action startsWithAssertion = () => text.ShouldStartWith("A", userMessage: Reason);
        Action endsWithAssertion = () => text.ShouldEndWith("C", userMessage: Reason);
        Action decomposedContainsAssertion = () => precomposed.ShouldContain(decomposed, userMessage: Reason);
        Action decomposedStartsWithAssertion = () => precomposed.ShouldStartWith(decomposed, userMessage: Reason);
        Action decomposedEndsWithAssertion = () => precomposed.ShouldEndWith(decomposed, userMessage: Reason);

        // Act
        var containsException = containsAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var startsWithException = startsWithAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var endsWithException = endsWithAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var notContainsException = Record.Exception(() => text.ShouldNotContain("B", userMessage: Reason));
        var decomposedContainsException = decomposedContainsAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var decomposedStartsWithException = decomposedStartsWithAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var decomposedEndsWithException = decomposedEndsWithAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var decomposedNotContainsException = Record.Exception(() => precomposed.ShouldNotContain(decomposed, userMessage: Reason));

        // Assert
        containsException.InnerException.ShouldBeOfType<Xunit.Sdk.ContainsException>();
        startsWithException.InnerException.ShouldBeOfType<Xunit.Sdk.StartsWithException>();
        endsWithException.InnerException.ShouldBeOfType<Xunit.Sdk.EndsWithException>();
        notContainsException.ShouldBeNull();
        decomposedContainsException.InnerException.ShouldBeOfType<Xunit.Sdk.ContainsException>();
        decomposedStartsWithException.InnerException.ShouldBeOfType<Xunit.Sdk.StartsWithException>();
        decomposedEndsWithException.InnerException.ShouldBeOfType<Xunit.Sdk.EndsWithException>();
        decomposedNotContainsException.ShouldBeNull();
    }

    [Fact]
    public async Task Async_message_overload_states_the_reason_before_the_awaited_failure()
    {
        // Arrange
        string? orderId = null;
        Func<Task> completes = async () => await Task.Yield();
        Func<Task> rejectsOrder = async () =>
        {
            await Task.Yield();
            ArgumentNullException.ThrowIfNull(orderId);
        };
        Func<Task> assignableAssertion = () => completes.ShouldThrowAssignableTo<InvalidOperationException>(Reason);
        Func<Task> typeAssertion = () => rejectsOrder.ShouldThrow<InvalidOperationException>(Reason);

        // Act
        var assignableException = await assignableAssertion.ShouldThrow<Xunit.Sdk.XunitException>();
        var typeException = await typeAssertion.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        var assignableFailure = assignableException.InnerException.ShouldBeOfType<Xunit.Sdk.ThrowsAnyException>();
        var typeFailure = typeException.InnerException.ShouldBeOfType<Xunit.Sdk.ThrowsException>();
        assignableException.Message.ShouldBe(Reason + Environment.NewLine + assignableFailure.Message);
        typeException.Message.ShouldBe(Reason + Environment.NewLine + typeFailure.Message);
    }

    [Fact]
    public async Task HTTP_message_overload_states_the_reason_before_the_problem_failure()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"status":400,"detail":"invalid id","code":"invalid"}""", Encoding.UTF8, "application/problem+json")
        };
        Func<Task> assertion = async () => await response.ShouldBeProblem(
            HttpStatusCode.NotFound,
            null,
            null,
            userMessage: Reason,
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var exception = await assertion.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        var failure = exception.InnerException.ShouldBeOfType<Xunit.Sdk.EqualException>();
        exception.Message.ShouldBe(Reason + Environment.NewLine + failure.Message);
    }

    [Fact]
    public async Task HTTP_message_overload_passes_cancellation_through_unchanged()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"status":400,"detail":"invalid id","code":"invalid","errors":{"Id":["required"]}}""", Encoding.UTF8, "application/problem+json")
        };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> problemAssertion = async () => await response.ShouldBeProblem(
            HttpStatusCode.BadRequest,
            null,
            null,
            userMessage: Reason,
            cancellationToken: cancellation.Token);
        Func<Task> validationAssertion = async () => await response.ShouldBeValidationProblem(
            null,
            null,
            userMessage: Reason,
            cancellationToken: cancellation.Token);

        // Act
        var problemCancellation = await problemAssertion.ShouldThrow<OperationCanceledException>();
        var validationCancellation = await validationAssertion.ShouldThrow<OperationCanceledException>();

        // Assert
        problemCancellation.CancellationToken.ShouldBe(cancellation.Token);
        validationCancellation.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Title_message_overload_validates_its_arguments()
    {
        // Arrange
        IPage missingPage = null!;
        var page = TitleAssertionPageProxy.Page;
        Func<Task> withoutPage = () => missingPage.ShouldHaveTitle("Bookings", userMessage: Reason);
        Func<Task> withoutMessage = () => page.ShouldHaveTitle("Bookings", userMessage: null!);
        Func<Task> withBlankTitle = () => page.ShouldHaveTitle(" ", userMessage: Reason);

        // Act
        var pageException = await withoutPage.ShouldThrow<ArgumentNullException>();
        var messageException = await withoutMessage.ShouldThrow<ArgumentNullException>();
        var titleException = await withBlankTitle.ShouldThrow<ArgumentException>();

        // Assert
        pageException.ParamName.ShouldBe("page");
        messageException.ParamName.ShouldBe("userMessage");
        titleException.ParamName.ShouldBe("expectedTitle");
    }
}
