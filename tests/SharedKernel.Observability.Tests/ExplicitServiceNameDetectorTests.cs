namespace PandaBear.Observability.Tests;

/// <summary>Tests the resource attributes produced by <see cref="ExplicitServiceNameDetector"/>.</summary>
public class ExplicitServiceNameDetectorTests
{
    [Fact]
    public void Detect_sets_service_name_attribute()
    {
        // Arrange
        var detector = new ExplicitServiceNameDetector("observable-app");

        // Act
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        // Assert
        dict.ContainsKey("service.name").ShouldBeTrue();
        dict["service.name"].ShouldBe("observable-app");
    }

    [Fact]
    public void Detect_sets_service_version_when_provided()
    {
        // Arrange
        var detector = new ExplicitServiceNameDetector("observable-app", "1.2.3");

        // Act
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        // Assert
        dict.ContainsKey("service.version").ShouldBeTrue();
        dict["service.version"].ShouldBe("1.2.3");
    }

    [Fact]
    public void Detect_does_not_set_service_version_when_whitespace()
    {
        // Arrange
        var detector = new ExplicitServiceNameDetector("observable-app", "  ");

        // Act
        var resource = detector.Detect();
        var dict = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        // Assert
        dict.ContainsKey("service.version").ShouldBeFalse();
    }
}
