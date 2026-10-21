using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class IbanTests
{
    // SWIFT IBAN Registry release 103: one electronic-format example per registered country.
    [Theory]
    [InlineData("AD1200012030200359100100")]
    [InlineData("AE070331234567890123456")]
    [InlineData("AL47212110090000000235698741")]
    [InlineData("AT611904300234573201")]
    [InlineData("AZ21NABZ00000000137010001944")]
    [InlineData("BA391290079401028494")]
    [InlineData("BE68539007547034")]
    [InlineData("BG80BNBG96611020345678")]
    [InlineData("BH67BMAG00001299123456")]
    [InlineData("BI4210000100010000332045181")]
    [InlineData("BR6699999A03000010009795493C1")]
    [InlineData("BY13NBRB3600900000002Z00AB00")]
    [InlineData("CH9300762011623852957")]
    [InlineData("CR05015202001026284066")]
    [InlineData("CY17002001280000001200527600")]
    [InlineData("CZ6508000000192000145399")]
    [InlineData("DE89370400440532013000")]
    [InlineData("DJ2100010000000154000100186")]
    [InlineData("DK5000400440116243")]
    [InlineData("DO28BAGR00000001212453611324")]
    [InlineData("EE382200221020145685")]
    [InlineData("EG380019000500000000263180002")]
    [InlineData("ES9121000418450200051332")]
    [InlineData("FI2112345600000785")]
    [InlineData("FK88SC123456789012")]
    [InlineData("FO6264600001631634")]
    [InlineData("FR1420041010050500013M02606")]
    [InlineData("GB29NWBK60161331926819")]
    [InlineData("GE29NB0000000101904917")]
    [InlineData("GI75NWBK000000007099453")]
    [InlineData("GL8964710001000206")]
    [InlineData("GR1601101250000000012300695")]
    [InlineData("GT82TRAJ01020000001210029690")]
    [InlineData("HN88CABF00000000000250005469")]
    [InlineData("HR1210010051863000160")]
    [InlineData("HU42117730161111101800000000")]
    [InlineData("IE29AIBK93115212345678")]
    [InlineData("IL620108000000099999999")]
    [InlineData("IQ98NBIQ850123456789012")]
    [InlineData("IS140159260076545510730339")]
    [InlineData("IT60X0542811101000000123456")]
    [InlineData("JO94CBJO0010000000000131000302")]
    [InlineData("KW81CBKU0000000000001234560101")]
    [InlineData("KZ86125KZT5004100100")]
    [InlineData("LB62099900000001001901229114")]
    [InlineData("LC55HEMM000100010012001200023015")]
    [InlineData("LI21088100002324013AA")]
    [InlineData("LT121000011101001000")]
    [InlineData("LU280019400644750000")]
    [InlineData("LV80BANK0000435195001")]
    [InlineData("LY83002048000020100120361")]
    [InlineData("MC5811222000010123456789030")]
    [InlineData("MD24AG000225100013104168")]
    [InlineData("ME25505000012345678951")]
    [InlineData("MK07250120000058984")]
    [InlineData("MN121234123456789123")]
    [InlineData("MR1300020001010000123456753")]
    [InlineData("MT84MALT011000012345MTLCAST001S")]
    [InlineData("MU17BOMM0101101030300200000MUR")]
    [InlineData("NI45BAPR00000013000003558124")]
    [InlineData("NL91ABNA0417164300")]
    [InlineData("NO9386011117947")]
    [InlineData("OM810180000001299123456")]
    [InlineData("PK36SCBL0000001123456702")]
    [InlineData("PL61109010140000071219812874")]
    [InlineData("PS92PALS000000000400123456702")]
    [InlineData("PT50000201231234567890154")]
    [InlineData("QA58DOHB00001234567890ABCDEFG")]
    [InlineData("RO49AAAA1B31007593840000")]
    [InlineData("RS35260005601001611379")]
    [InlineData("RU0304452522540817810538091310419")]
    [InlineData("SA0380000000608010167519")]
    [InlineData("SC18SSCB11010000000000001497USD")]
    [InlineData("SD2129010501234001")]
    [InlineData("SE4550000000058398257466")]
    [InlineData("SI56263300012039086")]
    [InlineData("SK3112000000198742637541")]
    [InlineData("SM86U0322509800000000270100")]
    [InlineData("SO211000001001000100141")]
    [InlineData("ST23000100010051845310146")]
    [InlineData("SV62CENR00000000000000700025")]
    [InlineData("TL380080012345678910157")]
    [InlineData("TN5910006035183598478831")]
    [InlineData("TR330006100519786457841326")]
    [InlineData("UA213223130000026007233566001")]
    [InlineData("VA59001123000012345678")]
    [InlineData("VG96VPVG0000012345678901")]
    [InlineData("XK051212012345678906")]
    [InlineData("YE15CBYE0001018861234567891234")]
    public void Creates_each_registered_country_example(string value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("AD1200012030200359100100")]
    [InlineData("AE070331234567890123456")]
    [InlineData("AL47212110090000000235698741")]
    [InlineData("AT611904300234573201")]
    [InlineData("AZ21NABZ00000000137010001944")]
    [InlineData("BA391290079401028494")]
    [InlineData("BE68539007547034")]
    [InlineData("BG80BNBG96611020345678")]
    [InlineData("BH67BMAG00001299123456")]
    [InlineData("BI4210000100010000332045181")]
    [InlineData("BR6699999A03000010009795493C1")]
    [InlineData("BY13NBRB3600900000002Z00AB00")]
    [InlineData("CH9300762011623852957")]
    [InlineData("CR05015202001026284066")]
    [InlineData("CY17002001280000001200527600")]
    [InlineData("CZ6508000000192000145399")]
    [InlineData("DE89370400440532013000")]
    [InlineData("DJ2100010000000154000100186")]
    [InlineData("DK5000400440116243")]
    [InlineData("DO28BAGR00000001212453611324")]
    [InlineData("EE382200221020145685")]
    [InlineData("EG380019000500000000263180002")]
    [InlineData("ES9121000418450200051332")]
    [InlineData("FI2112345600000785")]
    [InlineData("FK88SC123456789012")]
    [InlineData("FO6264600001631634")]
    [InlineData("FR1420041010050500013M02606")]
    [InlineData("GB29NWBK60161331926819")]
    [InlineData("GE29NB0000000101904917")]
    [InlineData("GI75NWBK000000007099453")]
    [InlineData("GL8964710001000206")]
    [InlineData("GR1601101250000000012300695")]
    [InlineData("GT82TRAJ01020000001210029690")]
    [InlineData("HN88CABF00000000000250005469")]
    [InlineData("HR1210010051863000160")]
    [InlineData("HU42117730161111101800000000")]
    [InlineData("IE29AIBK93115212345678")]
    [InlineData("IL620108000000099999999")]
    [InlineData("IQ98NBIQ850123456789012")]
    [InlineData("IS140159260076545510730339")]
    [InlineData("IT60X0542811101000000123456")]
    [InlineData("JO94CBJO0010000000000131000302")]
    [InlineData("KW81CBKU0000000000001234560101")]
    [InlineData("KZ86125KZT5004100100")]
    [InlineData("LB62099900000001001901229114")]
    [InlineData("LC55HEMM000100010012001200023015")]
    [InlineData("LI21088100002324013AA")]
    [InlineData("LT121000011101001000")]
    [InlineData("LU280019400644750000")]
    [InlineData("LV80BANK0000435195001")]
    [InlineData("LY83002048000020100120361")]
    [InlineData("MC5811222000010123456789030")]
    [InlineData("MD24AG000225100013104168")]
    [InlineData("ME25505000012345678951")]
    [InlineData("MK07250120000058984")]
    [InlineData("MN121234123456789123")]
    [InlineData("MR1300020001010000123456753")]
    [InlineData("MT84MALT011000012345MTLCAST001S")]
    [InlineData("MU17BOMM0101101030300200000MUR")]
    [InlineData("NI45BAPR00000013000003558124")]
    [InlineData("NL91ABNA0417164300")]
    [InlineData("NO9386011117947")]
    [InlineData("OM810180000001299123456")]
    [InlineData("PK36SCBL0000001123456702")]
    [InlineData("PL61109010140000071219812874")]
    [InlineData("PS92PALS000000000400123456702")]
    [InlineData("PT50000201231234567890154")]
    [InlineData("QA58DOHB00001234567890ABCDEFG")]
    [InlineData("RO49AAAA1B31007593840000")]
    [InlineData("RS35260005601001611379")]
    [InlineData("RU0304452522540817810538091310419")]
    [InlineData("SA0380000000608010167519")]
    [InlineData("SC18SSCB11010000000000001497USD")]
    [InlineData("SD2129010501234001")]
    [InlineData("SE4550000000058398257466")]
    [InlineData("SI56263300012039086")]
    [InlineData("SK3112000000198742637541")]
    [InlineData("SM86U0322509800000000270100")]
    [InlineData("SO211000001001000100141")]
    [InlineData("ST23000100010051845310146")]
    [InlineData("SV62CENR00000000000000700025")]
    [InlineData("TL380080012345678910157")]
    [InlineData("TN5910006035183598478831")]
    [InlineData("TR330006100519786457841326")]
    [InlineData("UA213223130000026007233566001")]
    [InlineData("VA59001123000012345678")]
    [InlineData("VG96VPVG0000012345678901")]
    [InlineData("XK051212012345678906")]
    [InlineData("YE15CBYE0001018861234567891234")]
    public void Rejects_changed_check_digits_for_each_registered_country(string value)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(value);
        var corrupted = value[..2] + (value[2] == '9' ? '0' : (char)(value[2] + 1)) + value[3..];

        // Act
        var result = Iban.Create(corrupted);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DE")]
    [InlineData("de89370400440532013000")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData(" DE89370400440532013000")]
    [InlineData("DE89370400440532013000\n")]
    [InlineData("DE８9370400440532013000")]
    [InlineData("DE8937040044053201300")]
    [InlineData("DE893704004405320130000")]
    public void Rejects_noncanonical_or_incomplete_candidates(string? value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("DE85A70400440532013000")]
    [InlineData("GB731EST12345698765432")]
    [InlineData("BR2999999A03A00010009795493C1")]
    [InlineData("FR462004A010050500013M02606")]
    [InlineData("ZZ22370400440532013000")]
    public void Rejects_country_or_structure_errors_even_with_recalculated_check_digits(string value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Rejects_oversized_candidates()
    {
        // Arrange
        var value = new string('A', 10_000);

        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("DE00000000000000000066")]
    [InlineData("DE01000000000000000048")]
    [InlineData("DE99000000000000000030")]
    public void Rejects_noncanonical_check_digit_ranges_even_when_modulo_is_one(string value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }
}
