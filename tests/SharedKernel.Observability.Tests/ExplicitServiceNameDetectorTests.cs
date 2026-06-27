namespace SharedKernel.Observability.Tests;

public class ExplicitServiceNameDetectorTests
{
    [Fact]
    public void Detect_sets_service_name_attribute()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.True(dict.ContainsKey("service.name"));
        Assert.Equal("observable-app", dict["service.name"]);
    }

    [Fact]
    public void Detect_sets_service_version_when_provided()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app", "1.2.3");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.True(dict.ContainsKey("service.version"));
        Assert.Equal("1.2.3", dict["service.version"]);
    }

    [Fact]
    public void Detect_does_not_set_service_version_when_whitespace()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app", "  ");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.False(dict.ContainsKey("service.version"));
    }
}
