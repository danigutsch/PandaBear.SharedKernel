namespace SharedKernel.Observability.Tests;

public class ExplicitServiceNameDetectorTests
{
    [Fact]
    public void Detect_Sets_Service_Name_Attribute()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.True(dict.ContainsKey("service.name"));
        Assert.Equal("observable-app", dict["service.name"]);
    }

    [Fact]
    public void Detect_Sets_Service_Version_When_Provided()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app", "1.2.3");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.True(dict.ContainsKey("service.version"));
        Assert.Equal("1.2.3", dict["service.version"]);
    }

    [Fact]
    public void Detect_Does_Not_Set_Service_Version_When_Whitespace()
    {
        var detector = new SharedKernel.Observability.ExplicitServiceNameDetector("observable-app", "  ");
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.False(dict.ContainsKey("service.version"));
    }
}
