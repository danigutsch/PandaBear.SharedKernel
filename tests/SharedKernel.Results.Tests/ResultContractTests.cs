namespace PandaBear.Results.Tests;

/// <summary>
/// Tests that Result and Result&lt;T&gt; satisfy the shared <see cref="IResultLike{TSelf}"/> contract generically.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ResultContractTests
{
    [Fact]
    public void Builds_invalid_with_field_for_result()
    {
        var result = ResultContractTestsHelpers.InvalidWithField<Result>();

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.TryGetError(out var error).ShouldBeTrue();
        error!.ValidationErrors!["name"].ShouldBe(["required"]);
    }

    [Fact]
    public void Builds_invalid_with_field_for_result_of_t()
    {
        var result = ResultContractTestsHelpers.InvalidWithField<Result<int>>();

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.TryGetError(out var error).ShouldBeTrue();
        error!.ValidationErrors!["name"].ShouldBe(["required"]);
    }

    [Fact]
    public void Builds_invalid_with_dictionary_for_both_shapes()
    {
        var errors = new Dictionary<string, string[]> { ["a"] = ["x"] };

        ResultContractTestsHelpers.InvalidFromDictionary<Result>(errors).ErrorDetails!.ValidationErrors!["a"].ShouldBe(["x"]);
        ResultContractTestsHelpers.InvalidFromDictionary<Result<string>>(errors).ErrorDetails!.ValidationErrors!["a"].ShouldBe(["x"]);
    }

    [Fact]
    public void Builds_not_found_and_conflict_for_both_shapes()
    {
        ResultContractTestsHelpers.NotFound<Result>("gone").Status.ShouldBe(ResultStatus.NotFound);
        ResultContractTestsHelpers.NotFound<Result<int>>("gone").Status.ShouldBe(ResultStatus.NotFound);
        ResultContractTestsHelpers.Conflict<Result>("clash").Status.ShouldBe(ResultStatus.Conflict);
        ResultContractTestsHelpers.Conflict<Result<int>>("clash").Status.ShouldBe(ResultStatus.Conflict);
    }

    [Theory]
    [InlineData(ResultErrorCodes.Invalid, ResultStatus.Invalid)]
    [InlineData(ResultErrorCodes.NotFound, ResultStatus.NotFound)]
    [InlineData(ResultErrorCodes.Unauthorized, ResultStatus.Unauthorized)]
    [InlineData(ResultErrorCodes.Forbidden, ResultStatus.Forbidden)]
    [InlineData(ResultErrorCodes.Conflict, ResultStatus.Conflict)]
    [InlineData(ResultErrorCodes.Unavailable, ResultStatus.Unavailable)]
    [InlineData(ResultErrorCodes.Error, ResultStatus.Error)]
    [InlineData(ResultErrorCodes.CriticalError, ResultStatus.CriticalError)]
    [InlineData("custom.code", ResultStatus.Error)]
    public void Builds_failure_from_existing_error_with_mapped_status(string code, ResultStatus expected)
    {
        var validation = code == ResultErrorCodes.Invalid
            ? new Dictionary<string, string[]> { ["f"] = ["m"] }
            : null;
        var error = new ResultError("detail", code, validation);

        var plain = Result.FromError(error);
        var generic = Result<int>.FromError(error);

        plain.Status.ShouldBe(expected);
        plain.ErrorDetails.ShouldBe(error);
        generic.Status.ShouldBe(expected);
        generic.ErrorDetails.ShouldBe(error);
    }

    [Fact]
    public void Rejects_invalid_error_without_field_details()
    {
        var error = new ResultError("detail", ResultErrorCodes.Invalid);

        ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<object?>)(() => Result.FromError(error))).ParamName.ShouldBe("resultError");
        ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<object?>)(() => Result<int>.FromError(error))).ParamName.ShouldBe("resultError");
        ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => Result.FromError(null!))).ParamName.ShouldBe("resultError");
        ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => Result<int>.FromError(null!))).ParamName.ShouldBe("resultError");
    }

    [Fact]
    public void Ensure_keeps_error_parameter_name_for_invalid_error_without_field_details()
    {
        var error = new ResultError("detail", ResultErrorCodes.Invalid);

        ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<object?>)(() => Result.Ok(1).Ensure(static _ => false, error))).ParamName.ShouldBe("error");
    }

    [Fact]
    public void Rejects_empty_validation_dictionary_through_the_interface()
    {
        var empty = new Dictionary<string, string[]>();

        ShouldAssertionExtensions.ShouldThrow<ArgumentOutOfRangeException>((Func<object?>)(() => ResultContractTestsHelpers.InvalidFromDictionary<Result>(empty)));
        ShouldAssertionExtensions.ShouldThrow<ArgumentOutOfRangeException>((Func<object?>)(() => ResultContractTestsHelpers.InvalidFromDictionary<Result<int>>(empty)));
    }

    [Fact]
    public void Builds_every_status_factory_through_the_interface()
    {
        ResultContractTestsHelpers.FactoryStatuses<Result>().ShouldBe(ResultContractTestsHelpers.ExpectedFactoryStatuses);
        ResultContractTestsHelpers.FactoryStatuses<Result<int>>().ShouldBe(ResultContractTestsHelpers.ExpectedFactoryStatuses);
    }

    [Fact]
    public void Default_results_are_uninitialized_through_the_interface()
    {
        ResultContractTestsHelpers.DefaultState<Result>().ShouldBe("Unknown:False:False");
        ResultContractTestsHelpers.DefaultState<Result<int>>().ShouldBe("Unknown:False:False");
    }

    [Fact]
    public void Reads_failure_state_generically()
    {
        ResultContractTestsHelpers.Describe(Result.Ok()).ShouldBe("success");
        ResultContractTestsHelpers.Describe(Result.Ok(1)).ShouldBe("success");
        ResultContractTestsHelpers.Describe(Result.NotFound("x")).ShouldBe("failure:x");
        ResultContractTestsHelpers.Describe(Result.NotFound<int>("y")).ShouldBe("failure:y");
    }

    [Fact]
    public async Task Open_generic_validation_behavior_short_circuits_for_both_response_shapes()
    {
        var plain = await ResultContractTestsHelpers.ValidationBehavior<Result>.Run(string.Empty, () => ValueTask.FromResult(Result.Ok()));
        var generic = await ResultContractTestsHelpers.ValidationBehavior<Result<int>>.Run(string.Empty, () => ValueTask.FromResult(Result.Ok(7)));
        var passed = await ResultContractTestsHelpers.ValidationBehavior<Result<int>>.Run("ok", () => ValueTask.FromResult(Result.Ok(7)));

        plain.Status.ShouldBe(ResultStatus.Invalid);
        generic.Status.ShouldBe(ResultStatus.Invalid);
        generic.ErrorDetails!.ValidationErrors!["name"].ShouldBe(["Name is required"]);
        passed.Value.ShouldBe(7);
    }
}
