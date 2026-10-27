namespace PandaBear.HttpClients.Tests;

/// <summary>
/// Unit tests for UploadOperationState progress reporting, cancellation, retry, and screen-reader announcements.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UploadOperationStateTests
{
    [Fact]
    public void Upload_operation_reports_only_authoritative_bytes_and_keeps_server_processing_indeterminate()
    {
        // Arrange
        var state = new UploadOperationState();
        var operationId = state.Begin("customers.csv", 100, isRetry: false);

        // Act
        state.ReportBytes(operationId, 25);

        // Assert
        state.Status.ShouldBe(UploadOperationStatus.Uploading);
        state.BytesTransferred.ShouldBe(25);
        state.TotalBytes.ShouldBe(100);
        state.IsDeterminate.ShouldBeTrue();

        // Act
        state.ReportBytes(operationId, 100);

        // Assert
        state.Status.ShouldBe(UploadOperationStatus.Processing);
        state.IsDeterminate.ShouldBeFalse();
    }

    [Fact]
    public void Upload_operation_ignores_callbacks_after_cancellation()
    {
        // Arrange
        var state = new UploadOperationState();
        var operationId = state.Begin("customers.csv", 100, isRetry: false);
        state.ReportBytes(operationId, 25);

        // Act
        state.Cancel(operationId);
        state.ReportBytes(operationId, 100);
        state.Complete(operationId);

        // Assert
        state.Status.ShouldBe(UploadOperationStatus.Canceled);
        state.BytesTransferred.ShouldBe(25);
    }

    [Fact]
    public void Retry_creates_a_new_operation_that_stale_callbacks_cannot_replace()
    {
        // Arrange
        var state = new UploadOperationState();
        var canceledOperationId = state.Begin("customers.csv", 100, isRetry: false);
        state.Cancel(canceledOperationId);

        // Act
        var retryOperationId = state.Begin("customers.csv", 100, isRetry: true);
        state.ReportBytes(canceledOperationId, 100);
        state.Complete(canceledOperationId);

        // Assert
        retryOperationId.ShouldNotBe(canceledOperationId);
        state.Status.ShouldBe(UploadOperationStatus.Retrying);
        state.BytesTransferred.ShouldBe(0);
    }

    [Fact]
    public void Upload_operation_rejects_a_second_active_operation()
    {
        // Arrange
        var state = new UploadOperationState();
        _ = state.Begin("customers.csv", 100, isRetry: false);

        // Act
        Action action = () => state.Begin("other.csv", 100, isRetry: false);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Upload_operation_rejects_completion_before_all_declared_bytes_are_reported()
    {
        // Arrange
        var state = new UploadOperationState();
        var operationId = state.Begin("customers.csv", 100, isRetry: false);
        state.ReportBytes(operationId, 50);

        // Act
        Action action = () => state.Complete(operationId);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Upload_announcements_are_throttled_between_percentage_boundaries()
    {
        // Arrange
        var state = new UploadOperationState();
        var operationId = state.Begin("customers.csv", 100, isRetry: false);

        // Act
        state.ReportBytes(operationId, 1);
        var firstAnnouncement = state.Announcement;
        state.ReportBytes(operationId, 2);
        var secondAnnouncement = state.Announcement;
        state.ReportBytes(operationId, 10);

        // Assert
        firstAnnouncement.ShouldBe("Uploading customers.csv.");
        secondAnnouncement.ShouldBe(firstAnnouncement);
        state.Announcement.ShouldBe("customers.csv upload is 10% complete.");
    }

    [Fact]
    public void First_percentage_announcement_is_not_replaced_with_a_less_informative_status()
    {
        // Arrange
        var state = new UploadOperationState();
        var operationId = state.Begin("customers.csv", 100, isRetry: false);

        // Act
        state.ReportBytes(operationId, 10);
        var boundaryAnnouncement = state.Announcement;
        state.ReportBytes(operationId, 11);

        // Assert
        boundaryAnnouncement.ShouldBe("customers.csv upload is 10% complete.");
        state.Announcement.ShouldBe(boundaryAnnouncement);
    }

    [Theory]
    [InlineData(long.MaxValue / 2, "customers.csv upload is 40% complete.")]
    [InlineData(long.MaxValue - 1, "customers.csv upload is 90% complete.")]
    [InlineData(long.MaxValue / 10, "Uploading customers.csv.")]
    [InlineData(long.MaxValue / 10 + 1, "customers.csv upload is 10% complete.")]
    public void Large_byte_counts_preserve_integer_percentage_boundaries(long bytesTransferred, string announcement)
    {
        // Arrange
        var state = new UploadOperationState();
        var operationId = state.Begin("customers.csv", long.MaxValue, isRetry: false);

        // Act
        state.ReportBytes(operationId, bytesTransferred);

        // Assert
        state.Announcement.ShouldBe(announcement);
        state.Status.ShouldBe(UploadOperationStatus.Uploading);
    }

}
