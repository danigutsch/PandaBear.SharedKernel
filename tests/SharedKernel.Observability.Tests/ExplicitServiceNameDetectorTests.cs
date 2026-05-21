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
}
