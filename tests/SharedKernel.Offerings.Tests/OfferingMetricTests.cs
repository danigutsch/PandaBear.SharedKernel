using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the application-defined metrics on the offering and on its steps.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingMetricTests
{
    [Fact]
    public void Metrics_are_kept_in_rule_order_on_the_offering_and_its_steps()
    {
        // Act
        var offering = Valid(Tour() with
        {
            Metrics = [new(TourMetric.Difficulty, 2m, 3m), new(TourMetric.Distance, 320.5m, Approximate: true)],
            Steps =
            [
                new TourStep(null, [new(TourMetric.ElevationGain, 850m), new(TourMetric.Distance, 45.5m), new(TourMetric.Transfer, 12m)]),
                new TourStep(null),
                new TourStep(null),
            ],
        });

        // Assert
        offering.Metrics.ShouldBe([new Metric<TourMetric>(TourMetric.Distance, 320.5m, null, true), new Metric<TourMetric>(TourMetric.Difficulty, 2m, 3m)]);
        offering.Steps[0].Metrics.Select(metric => metric.Kind).ShouldBe([TourMetric.Distance, TourMetric.Transfer, TourMetric.ElevationGain]);
        offering.Steps[1].Metrics.ShouldBeEmpty();
    }

    [Fact]
    public void A_kind_outside_the_rules_is_refused_on_the_list()
    {
        // Act
        var offering = Refused(Tour() with { Metrics = [new((TourMetric)42, 1m)] });
        var step = Refused(Tour() with { Steps = [new TourStep(null, [new((TourMetric)42, 1m)]), new TourStep(null), new TourStep(null)] });

        // Assert
        offering.ShouldHaveValidationError(OfferingFields.Metrics, "A metric kind is not supported.");
        step.ShouldHaveValidationError(OfferingFields.StepMetrics(0), "A metric kind is not supported.");
    }

    [Fact]
    public void A_metric_is_given_only_where_its_scope_allows()
    {
        // Act
        var onOffering = Refused(Tour() with { Metrics = [new(TourMetric.Transfer, 12m)] });
        var onStep = Refused(Tour() with { Steps = [new TourStep(null), new TourStep(null, [new(TourMetric.Difficulty, 2m)]), new TourStep(null)] });

        // Assert
        onOffering.ShouldHaveValidationError("metrics.transfer", "This metric is not allowed here.");
        onStep.ShouldHaveValidationError("steps[1].metrics.difficulty", "This metric is not allowed here.");
    }

    [Fact]
    public void A_kind_appears_once()
    {
        // Act
        var error = Refused(Tour() with { Metrics = [new(TourMetric.Distance, 300m), new(TourMetric.Distance, 320m)] });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Metric(TourMetric.Distance), "This metric can appear only once.");
    }

    [Theory]
    [InlineData("0", "The value must be between 1 and 5.")]
    [InlineData("6", "The value must be between 1 and 5.")]
    [InlineData("2.5", "The value can have at most 0 decimal places.")]
    public void The_value_is_within_the_bounds_and_decimals(string value, string message)
    {
        // Act
        var error = Refused(Tour() with { Metrics = [new(TourMetric.Difficulty, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))] });

        // Assert
        error.ShouldHaveValidationError("metrics.difficulty", message);
    }

    [Fact]
    public void Trailing_zeros_are_not_extra_decimals()
    {
        // Act
        var offering = Valid(Tour() with { Metrics = [new(TourMetric.Difficulty, 2.00m), new(TourMetric.Distance, 320.50m)] });

        // Assert
        offering.Metrics.Count.ShouldBe(2);
    }

    [Fact]
    public void A_range_needs_a_kind_that_allows_one_and_an_upper_value_above_the_value_and_within_the_maximum()
    {
        // Act
        var notAllowed = Refused(Tour() with { Metrics = [new(TourMetric.Distance, 300m, 320m)] });
        var notAbove = Refused(Tour() with { Metrics = [new(TourMetric.Difficulty, 3m, 3m)] });
        var tooHigh = Refused(Tour() with { Metrics = [new(TourMetric.Difficulty, 3m, 6m)] });

        // Assert
        notAllowed.ShouldHaveValidationError(OfferingFields.MetricUpper(TourMetric.Distance), "This metric does not allow a range.");
        notAbove.ShouldHaveValidationError("metrics.difficulty.upper", "The upper value must be greater than the value.");
        tooHigh.ShouldHaveValidationError("metrics.difficulty.upper", "The upper value must be between 1 and 5.");
    }

    [Fact]
    public void Approximate_needs_a_kind_that_allows_it()
    {
        // Act
        var error = Refused(Tour() with { Metrics = [new(TourMetric.Difficulty, 2m, Approximate: true)] });
        var step = Refused(Tour() with { Steps = [new TourStep(null, [new(TourMetric.Transfer, 2m, Approximate: true)]), new TourStep(null), new TourStep(null)] });

        // Assert
        error.ShouldHaveValidationError("metrics.difficulty", "This metric cannot be approximate.");
        step.ShouldHaveValidationError(OfferingFields.StepMetric(0, TourMetric.Transfer), "This metric cannot be approximate.");
    }

    [Fact]
    public void A_step_metric_range_has_its_own_upper_path()
    {
        // Arrange
        var rules = new TourRules(Cultures, PriceKinds, [new(TourMetric.Distance, MetricScope.Step, 0m, 100m, AllowsRange: true)]);
        var definition = Tour() with { Metrics = [], Steps = [new TourStep(null, [new(TourMetric.Distance, 40m, 30m)])] };

        // Act
        var result = Offering.Create(definition, rules, new ImageLibrary());

        // Assert
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.StepMetricUpper(0, TourMetric.Distance), "The upper value must be greater than the value.");
        OfferingFields.StepMetricUpper(0, TourMetric.Distance).ShouldBe("steps[0].metrics.distance.upper");
    }
}
