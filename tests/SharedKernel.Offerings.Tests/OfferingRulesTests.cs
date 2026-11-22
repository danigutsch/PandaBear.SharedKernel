using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the application's offering rules and the field paths of errors.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingRulesTests
{
    [Fact]
    public void Rules_have_defaults_for_every_limit()
    {
        // Act
        var rules = new TourRules(Cultures, PriceKinds, Metrics);

        // Assert
        rules.Cultures.ShouldBe(Cultures);
        rules.PriceKinds.ShouldBe(PriceKinds);
        rules.Metrics.ShouldBe(Metrics);
        rules.MaxDurationDays.ShouldBe(365);
        (rules.MinYear, rules.MaxYear).ShouldBe((2000, 2100));
        (rules.MaxTitleLength, rules.MaxLineLength, rules.MaxSummaryLength, rules.MaxDateNoteLength).ShouldBe((200, 300, 1000, 100));
        rules.MaxSlugLength.ShouldBe(Slug.DefaultMaxLength);
        (rules.MaxParagraphs, rules.MaxParagraphLength).ShouldBe((50, 4000));
        (rules.MaxPrices, rules.MaxListItems, rules.MaxSteps, rules.MaxGalleryImages, rules.MaxParticipants).ShouldBe((20, 60, 60, 60, 200));
        rules.MaxPrice.ShouldBe(1_000_000m);
        rules.StepCount.ShouldBe(OfferingStepCount.Free);
        rules.HighlightsCount.ShouldBeNull();
        rules.ForbiddenStepTitlePrefixes.ShouldBeEmpty();
    }

    [Fact]
    public void Kinds_must_be_defined_and_appear_once()
    {
        // Act
        Action twicePrice = () => _ = new TourRules(Cultures, [new(TourPriceKind.Other), new(TourPriceKind.Other)], Metrics);
        Action undefinedPrice = () => _ = new TourRules(Cultures, [new((TourPriceKind)42)], Metrics);
        Action twiceMetric = () => _ = new TourRules(Cultures, PriceKinds, [Metrics[0], Metrics[0]]);
        Action undefinedMetric = () => _ = new TourRules(Cultures, PriceKinds, [new((TourMetric)42, MetricScope.Step, 0m, 1m)]);

        // Assert
        twicePrice.ShouldThrow<ArgumentException>();
        undefinedPrice.ShouldThrow<ArgumentException>();
        twiceMetric.ShouldThrow<ArgumentException>();
        undefinedMetric.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(MetricScope.None, 0, 1, 0)]
    [InlineData((MetricScope)4, 0, 1, 0)]
    [InlineData(MetricScope.Step, 2, 1, 0)]
    [InlineData(MetricScope.Step, 0, 1, -1)]
    [InlineData(MetricScope.Step, 0, 1, 29)]
    public void A_metric_rule_needs_a_scope_ordered_bounds_and_sane_decimals(MetricScope scope, int min, int max, int decimals)
    {
        // Act
        Action act = () => _ = new TourRules(Cultures, PriceKinds, [new(TourMetric.Distance, scope, min, max, decimals)]);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Limits_must_be_positive_and_options_defined()
    {
        // Act
        Action duration = () => _ = new TourRules(Cultures, PriceKinds, Metrics) { MaxDurationDays = 0 };
        Action price = () => _ = new TourRules(Cultures, PriceKinds, Metrics) { MaxPrice = 0m };
        Action stepCount = () => _ = new TourRules(Cultures, PriceKinds, Metrics) { StepCount = (OfferingStepCount)5 };
        Action blankPrefix = () => _ = new TourRules(Cultures, PriceKinds, Metrics) { ForbiddenStepTitlePrefixes = ["Dia", " "] };
        Action highlights = () => _ = new CountRange(3, 2);
        Action noCultures = () => _ = new TourRules(null!, PriceKinds, Metrics);
        Action nullRule = () => _ = new TourRules(Cultures, [null!], Metrics);

        // Assert
        duration.ShouldThrow<ArgumentOutOfRangeException>();
        price.ShouldThrow<ArgumentOutOfRangeException>();
        stepCount.ShouldThrow<ArgumentOutOfRangeException>();
        blankPrefix.ShouldThrow<ArgumentException>();
        highlights.ShouldThrow<ArgumentOutOfRangeException>();
        noCultures.ShouldThrow<ArgumentNullException>();
        nullRule.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Field_paths_name_list_positions_metric_kinds_in_camel_case_and_translation_cultures()
    {
        // Act & Assert
        OfferingFields.PriceKind(2).ShouldBe("prices[2].kind");
        OfferingFields.PriceAmount(0).ShouldBe("prices[0].amount");
        OfferingFields.Metric(TourMetric.ElevationGain).ShouldBe("metrics.elevationGain");
        OfferingFields.MetricUpper(TourMetric.Difficulty).ShouldBe("metrics.difficulty.upper");
        OfferingFields.Metric((TourMetric)42).ShouldBe("metrics.42");
        OfferingFields.StepImage(3).ShouldBe("steps[3].imageId");
        OfferingFields.StepMetrics(1).ShouldBe("steps[1].metrics");
        OfferingFields.StepMetric(1, TourMetric.Transfer).ShouldBe("steps[1].metrics.transfer");
        OfferingFields.Of(English, OfferingFields.StepTitle(2)).ShouldBe("en.steps[2].title");
        OfferingFields.Of(Portuguese, OfferingFields.StepText(0)).ShouldBe("pt.steps[0].text");
        OfferingFields.Of(Portuguese, OfferingFields.StepNote(0)).ShouldBe("pt.steps[0].note");
        OfferingFields.Of(ContentCulture.Restore("pt-BR"), OfferingFields.Images).ShouldBe("pt-BR.images");
    }

    [Fact]
    public void Every_documented_field_path_is_a_constant()
    {
        // Act
        string[] paths =
        [
            OfferingFields.SortOrder, OfferingFields.Availability, OfferingFields.StartDate, OfferingFields.EndDate, OfferingFields.Year,
            OfferingFields.DurationDays, OfferingFields.SignUpLink, OfferingFields.Currency, OfferingFields.Prices, OfferingFields.ParticipantsMin,
            OfferingFields.ParticipantsMax, OfferingFields.GroupConditionMinimumParticipants, OfferingFields.GroupConditionSurchargePerMissing,
            OfferingFields.GroupConditionDueDays, OfferingFields.PaymentTermsDepositPercent, OfferingFields.PaymentTermsInstalments,
            OfferingFields.PaymentTermsBalanceDueDays, OfferingFields.Metrics, OfferingFields.Steps, OfferingFields.CoverId, OfferingFields.GalleryIds,
            OfferingFields.Translations, OfferingFields.Title, OfferingFields.Slug, OfferingFields.Status, OfferingFields.Subtitle, OfferingFields.Eyebrow,
            OfferingFields.Summary, OfferingFields.Description, OfferingFields.DateNote, OfferingFields.SeoTitle, OfferingFields.SeoDescription,
            OfferingFields.Highlights, OfferingFields.Included, OfferingFields.NotIncluded, OfferingFields.PracticalInfo, OfferingFields.PriceLabels,
            OfferingFields.Images,
        ];

        // Assert
        paths.ShouldBe(
        [
            "sortOrder", "availability", "startDate", "endDate", "year", "durationDays", "signUpLink", "currency", "prices", "participants.min",
            "participants.max", "groupCondition.minimumParticipants", "groupCondition.surchargePerMissing", "groupCondition.dueDays",
            "paymentTerms.depositPercent", "paymentTerms.instalments", "paymentTerms.balanceDueDays", "metrics", "steps", "coverId", "galleryIds",
            "translations", "title", "slug", "status", "subtitle", "eyebrow", "summary", "description", "dateNote", "seoTitle", "seoDescription",
            "highlights", "included", "notIncluded", "practicalInfo", "priceLabels", "images",
        ]);
    }

    [Fact]
    public void A_sign_up_link_must_be_a_web_address()
    {
        // Act
        var error = Refused(Tour() with { SignUpLink = "javascript:alert(1)" });

        // Assert
        Fields(error).ShouldBe([OfferingFields.SignUpLink]);
    }
}
