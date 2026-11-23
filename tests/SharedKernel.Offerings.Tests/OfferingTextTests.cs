using System.Globalization;
using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the display sentences of an offering, in Brazilian Portuguese and English, with the application's formats.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingTextTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");

    private static readonly DateRangePatterns PortugueseDates = new(
        "{0:d 'de' MMMM 'de' yyyy}", "{0:%d} a {1:d 'de' MMMM 'de' yyyy}", "{0:d 'de' MMMM} a {1:d 'de' MMMM 'de' yyyy}", "{0:d 'de' MMMM 'de' yyyy} a {1:d 'de' MMMM 'de' yyyy}");

    private static readonly DateRangePatterns EnglishDates = new("{0:d MMMM yyyy}", "{0:%d}–{1:d MMMM yyyy}", "{0:d MMMM} – {1:d MMMM yyyy}", "{0:d MMMM yyyy} – {1:d MMMM yyyy}");

    private static readonly MoneyDisplayOptions Euros = new() { SymbolPlacement = MoneySymbolPlacement.After, OmitZeroMinorUnits = true };

    [Fact]
    public void The_date_line_shows_the_dates_in_the_shared_format()
    {
        // Arrange
        var schedule = new OfferingSchedule(OfferingAvailability.Scheduled, DateOnlyRange.Create(new DateOnly(2027, 9, 20), new DateOnly(2027, 9, 29)).Value, 2027, 10);

        // Act
        var portuguese = OfferingText.DateLine(schedule, null, PortugueseDates, "Datas a confirmar, {0}", PtBr);
        var english = OfferingText.DateLine(schedule, "ignored", EnglishDates, "Dates to be confirmed, {0}", En);

        // Assert
        portuguese.ShouldBe("20 a 29 de setembro de 2027");
        english.ShouldBe("20–29 September 2027");
    }

    [Fact]
    public void Without_dates_the_line_is_the_note_else_the_year_to_be_confirmed_else_nothing()
    {
        // Arrange
        var toBeConfirmed = new OfferingSchedule(OfferingAvailability.DatesToBeConfirmed, null, 2028, 8);
        var onRequest = new OfferingSchedule(OfferingAvailability.OnRequest, null, null, 8);

        // Act
        var note = OfferingText.DateLine(toBeConfirmed, "Carnaval 2028", PortugueseDates, "Datas a confirmar, {0}", PtBr);
        var year = OfferingText.DateLine(toBeConfirmed, " ", PortugueseDates, "Datas a confirmar, {0}", PtBr);
        var english = OfferingText.DateLine(toBeConfirmed, null, EnglishDates, "Dates to be confirmed, {0}", En);
        var nothing = OfferingText.DateLine(onRequest, null, PortugueseDates, "Datas a confirmar, {0}", PtBr);
        var requestNote = OfferingText.DateLine(onRequest, "Sob medida", PortugueseDates, "Datas a confirmar, {0}", PtBr);

        // Assert
        note.ShouldBe("Carnaval 2028");
        year.ShouldBe("Datas a confirmar, 2028");
        english.ShouldBe("Dates to be confirmed, 2028");
        nothing.ShouldBeNull();
        requestNote.ShouldBe("Sob medida");
    }

    [Fact]
    public void The_date_line_of_a_view_reads_its_schedule_and_note()
    {
        // Arrange
        var view = Valid(Tour(Translation(status: PublicationStatus.Published))).PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Act
        var line = OfferingText.DateLine(view, PortugueseDates, "Datas a confirmar, {0}", PtBr);

        // Assert
        line.ShouldBe("20 a 22 de setembro de 2027");
    }

    [Theory]
    [InlineData(8, "pt-BR", new[] { "8 dias", "7 noites" })]
    [InlineData(2, "pt-BR", new[] { "2 dias", "1 noite" })]
    [InlineData(1, "pt-BR", new[] { "1 dia" })]
    [InlineData(8, "en", new[] { "8 days", "7 nights" })]
    [InlineData(1, "en", new[] { "1 day" })]
    public void The_duration_counts_days_and_nights_with_one_and_other_forms(int days, string culture, string[] expected)
    {
        // Arrange
        var schedule = new OfferingSchedule(OfferingAvailability.OnRequest, null, null, days);
        var (dayFormat, nightFormat) = culture == "en"
            ? (new PluralFormat("{0} day", "{0} days"), new PluralFormat("{0} night", "{0} nights"))
            : (new PluralFormat("{0} dia", "{0} dias"), new PluralFormat("{0} noite", "{0} noites"));

        // Act
        var duration = OfferingText.Duration(schedule, dayFormat, nightFormat, CultureInfo.GetCultureInfo(culture));

        // Assert
        duration.ShouldBe(expected);
    }

    [Theory]
    [InlineData(6, 16, "6 a 16 participantes")]
    [InlineData(6, null, "mínimo de 6 participantes")]
    [InlineData(null, 1, "até 1 participante")]
    [InlineData(12, 12, "12 participantes")]
    public void Participants_are_shown_as_a_range_a_minimum_a_maximum_or_an_exact_size(int? min, int? max, string expected)
    {
        // Arrange
        var formats = new ParticipantFormats(
            "{0} a {1} participantes",
            new PluralFormat("mínimo de {0} participante", "mínimo de {0} participantes"),
            new PluralFormat("até {0} participante", "até {0} participantes"),
            new PluralFormat("{0} participante", "{0} participantes"));

        // Act
        var text = OfferingText.Participants(new ParticipantRange(min, max), formats, PtBr);

        // Assert
        text.ShouldBe(expected);
    }

    [Fact]
    public void No_group_size_has_no_participants_text()
    {
        // Arrange
        var formats = new ParticipantFormats("{0}–{1}", new PluralFormat("{0}", "{0}"), new PluralFormat("{0}", "{0}"), new PluralFormat("{0}", "{0}"));

        // Act
        var missing = OfferingText.Participants(null, formats, En);
        var empty = OfferingText.Participants(new ParticipantRange(null, null), formats, En);

        // Assert
        missing.ShouldBeNull();
        empty.ShouldBeNull();
    }

    [Fact]
    public void The_group_condition_fills_the_application_sentence_with_the_displayed_surcharge()
    {
        // Arrange
        var condition = new GroupCondition(8, Money.Restore(150m, Currency.Eur), 45);
        var single = new GroupCondition(1, Money.Restore(150m, Currency.Eur), 1);
        var portugueseFormats = new GroupConditionFormats(
            "Valores para no mínimo {0}; com menos, {1} por pessoa a menos, confirmado {2} antes.",
            new PluralFormat("{0} participante", "{0} participantes"),
            new PluralFormat("{0} dia", "{0} dias"));
        var englishFormats = new GroupConditionFormats(
            "Prices for at least {0}; {1} more per missing person, confirmed {2} before.",
            new PluralFormat("{0} person", "{0} people"),
            new PluralFormat("{0} day", "{0} days"));

        // Act
        var portuguese = OfferingText.GroupCondition(condition, portugueseFormats, Euros, PtBr);
        var one = OfferingText.GroupCondition(single, portugueseFormats, Euros, PtBr);
        var english = OfferingText.GroupCondition(condition, englishFormats, new MoneyDisplayOptions(), CultureInfo.GetCultureInfo("en-US"));

        // Assert
        portuguese.ShouldBe("Valores para no mínimo 8 participantes; com menos, 150 € por pessoa a menos, confirmado 45 dias antes.");
        one.ShouldBe("Valores para no mínimo 1 participante; com menos, 150 € por pessoa a menos, confirmado 1 dia antes.");
        english.ShouldBe("Prices for at least 8 people; €150.00 more per missing person, confirmed 45 days before.");
    }

    [Fact]
    public void Payment_terms_give_one_sentence_per_given_part()
    {
        // Arrange
        var formats = new PaymentTermsFormats("Sinal de {0}%", new PluralFormat("em {0} parcela", "em até {0} parcelas"), new PluralFormat("saldo {0} dia antes", "saldo {0} dias antes"));

        // Act
        var all = OfferingText.PaymentTerms(new PaymentTerms(30, 10, 60), formats, PtBr);
        var single = OfferingText.PaymentTerms(new PaymentTerms(null, 1, null), formats, PtBr);

        // Assert
        all.ShouldBe(["Sinal de 30%", "em até 10 parcelas", "saldo 60 dias antes"]);
        single.ShouldBe(["em 1 parcela"]);
    }

    [Theory]
    [InlineData("320.5", null, true, "pt-BR", "aprox. 320,5 km")]
    [InlineData("1200", null, false, "pt-BR", "1.200 km")]
    [InlineData("1200", null, false, "en", "1,200 km")]
    [InlineData("45.0", null, false, "en", "45 km")]
    [InlineData("2", "3", false, "pt-BR", "2–3 km")]
    public void A_metric_shows_its_approximate_prefix_its_range_with_an_en_dash_and_its_unit(string value, string? upper, bool approximate, string culture, string expected)
    {
        // Arrange
        var metric = new Metric<TourMetric>(
            TourMetric.Distance,
            decimal.Parse(value, CultureInfo.InvariantCulture),
            upper is null ? null : decimal.Parse(upper, CultureInfo.InvariantCulture),
            approximate);
        var format = new MetricFormat("{0} km", "aprox. {0}", Decimals: 1);

        // Act
        var text = OfferingText.Metric(metric, format, CultureInfo.GetCultureInfo(culture));

        // Assert
        text.ShouldBe(expected);
    }

    [Fact]
    public void An_unspecified_availability_is_invalid_input_for_the_sentences()
    {
        // Arrange
        var schedule = new OfferingSchedule(OfferingAvailability.Unspecified, null, 2028, 3);

        // Act
        Action dateLine = () => OfferingText.DateLine(schedule, null, PortugueseDates, "Datas a confirmar, {0}", PtBr);
        Action duration = () => OfferingText.Duration(schedule, new PluralFormat("{0} dia", "{0} dias"), new PluralFormat("{0} noite", "{0} noites"), PtBr);

        // Assert
        dateLine.ShouldThrow<ArgumentException>();
        duration.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_plural_format_picks_one_for_one_and_other_for_every_other_count()
    {
        // Arrange
        var format = new PluralFormat("{0} day", "{0} days");
        int[] counts = [0, 1, 2];

        // Act
        var forms = counts.Select(count => format.Format(count, En));

        // Assert
        forms.ShouldBe(["0 days", "1 day", "2 days"]);
    }

    [Fact]
    public void Formats_refuse_missing_or_blank_patterns()
    {
        // Act
        Action plural = () => _ = new PluralFormat(" ", "{0}");
        Action metric = () => _ = new MetricFormat("{0}", "{0}", Decimals: -1);
        Action participants = () => _ = new ParticipantFormats(null!, new("{0}", "{0}"), new("{0}", "{0}"), new("{0}", "{0}"));
        Action group = () => _ = new GroupConditionFormats("{0}", null!, new("{0}", "{0}"));

        // Assert
        plural.ShouldThrow<ArgumentException>();
        metric.ShouldThrow<ArgumentOutOfRangeException>();
        participants.ShouldThrow<ArgumentNullException>();
        group.ShouldThrow<ArgumentNullException>();
    }
}
