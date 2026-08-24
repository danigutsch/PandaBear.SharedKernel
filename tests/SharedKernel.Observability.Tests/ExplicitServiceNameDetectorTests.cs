namespace SharedKernel.Observability.Tests;

public class ExplicitServiceNameDetectorTests
{
    [Fact]
    public void Detect_sets_service_name_attribute()
    {
        var detector = new ExplicitServiceNameDetector("observable-app");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        (dict.ContainsKey("service.name")).ShouldBeTrue();
        (dict["service.name"]).ShouldBe("observable-app");
    }

    [Fact]
    public void Detect_sets_service_version_when_provided()
    {
        var detector = new ExplicitServiceNameDetector("observable-app", "1.2.3");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        (dict.ContainsKey("service.version")).ShouldBeTrue();
        (dict["service.version"]).ShouldBe("1.2.3");
    }

    [Fact]
    public void Detect_does_not_set_service_version_when_whitespace()
    {
        var detector = new ExplicitServiceNameDetector("observable-app", "  ");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        (dict.ContainsKey("service.version")).ShouldBeFalse();
    }
}
