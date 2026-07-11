namespace SharedKernel.Observability.Tests;

public class ExplicitServiceNameDetectorTests
{
    [Fact]
    public void Detect_sets_service_name_attribute()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        TestAssert.True(dict.ContainsKey("service.name"));
        TestAssert.Equal("observable-app", dict["service.name"]);
    }

    [Fact]
    public void Detect_sets_service_version_when_provided()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app", "1.2.3");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        TestAssert.True(dict.ContainsKey("service.version"));
        TestAssert.Equal("1.2.3", dict["service.version"]);
    }

    [Fact]
    public void Detect_does_not_set_service_version_when_whitespace()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app", "  ");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        TestAssert.False(dict.ContainsKey("service.version"));
    }
}
