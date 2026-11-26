namespace SharedKernel.RepoConfig.Tests;

/// <summary>Verifies Linux budget sampling with synthetic kernel and filesystem readings.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class LinuxResourceBudgetSamplerTests
{
    private const string UnifiedRootMount = "28 22 0:25 / /sys/fs/cgroup rw - cgroup2 cgroup rw\n";

    [Fact]
    public void Reports_effective_ancestor_headroom_and_selected_filesystem()
    {
        // Arrange
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["/proc/meminfo"] = "MemAvailable:      200 kB\nShmem:   7 kB\n",
            ["/proc/self/cgroup"] = "0::/user.slice/validation.scope\n",
            ["/proc/self/mountinfo"] = "28 22 0:25 / /sys/fs/cgroup rw - cgroup2 cgroup rw\n",
            ["/sys/fs/cgroup/user.slice/memory.max"] = "10000\n",
            ["/sys/fs/cgroup/user.slice/memory.current"] = "8500\n",
            ["/sys/fs/cgroup/user.slice/validation.scope/memory.max"] = "5000\n",
            ["/sys/fs/cgroup/user.slice/validation.scope/memory.current"] = "2000\n",
            ["/proc/pressure/memory"] = "some avg10=1.25 avg60=0.20 avg300=0.00 total=2210\nfull avg10=0.50 avg60=0.10 avg300=0.00 total=335\n",
        };

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(path => files.GetValueOrDefault(path), static () => (123456L, "ext4"));

        // Assert
        sample.HostAvailableBytes.ShouldBe(204800L);
        sample.ShmemBytes.ShouldBe(7168L);
        sample.CgroupMeasured.ShouldBeTrue();
        sample.CgroupHeadroomBytes.ShouldBe(1500L);
        sample.ScratchAvailableBytes.ShouldBe(123456L);
        sample.ScratchFilesystem.ShouldBe("ext4");
        sample.MemoryPressurePercent.ShouldBe(1.25m);
    }

    [Theory]
    [InlineData("max", null, true, null)]
    [InlineData("50", "75", true, 0)]
    [InlineData("50", null, false, null)]
    [InlineData("malformed", "5", false, null)]
    [InlineData(null, null, false, null)]
    public void Distinguishes_unlimited_headroom_from_unavailable_measurements(string? limit, string? current, bool measured, int? headroom)
    {
        // Arrange
        Dictionary<string, string?> files = new(StringComparer.Ordinal)
        {
            ["/proc/self/cgroup"] = "0::/validation.scope\n",
            ["/proc/self/mountinfo"] = "28 22 0:25 / /sys/fs/cgroup rw - cgroup2 cgroup rw\n",
            ["/sys/fs/cgroup/validation.scope/memory.max"] = limit,
            ["/sys/fs/cgroup/validation.scope/memory.current"] = current,
        };

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(path => files.GetValueOrDefault(path), static () => (100L, "tmpfs"));

        // Assert
        sample.CgroupMeasured.ShouldBe(measured);
        sample.CgroupHeadroomBytes.ShouldBe((long?)headroom);
    }

    [Theory]
    [InlineData(null, UnifiedRootMount)]
    [InlineData("0::/scope\n", null)]
    [InlineData("0::/scope\n", "28 22 0:25 / /sys/fs/cgroup rw - tmpfs tmpfs rw\n")]
    [InlineData("0::/scope\n", "28 22 0:25 /user.slice /sys/fs/cgroup rw - cgroup2 cgroup rw\n")]
    [InlineData("0::/scope\n", "28 22 0:25 / /wrong-mount rw - cgroup2 cgroup rw\n")]
    [InlineData("0::/scope\n2:memory:/scope\n", UnifiedRootMount)]
    [InlineData("0::/scope//nested\n", UnifiedRootMount)]
    [InlineData("0::/scope/./nested\n", UnifiedRootMount)]
    [InlineData("0::/scope/../nested\n", UnifiedRootMount)]
    public void Unsupported_or_unavailable_memory_hierarchy_retains_the_independent_readings(string? membership, string? mountInfo)
    {
        // Arrange
        Dictionary<string, string?> files = new(StringComparer.Ordinal)
        {
            ["/proc/meminfo"] = "MemAvailable: 200 kB\nShmem: 7 kB\n",
            ["/proc/self/cgroup"] = membership,
            ["/proc/self/mountinfo"] = mountInfo,
        };

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(path => files.GetValueOrDefault(path), static () => (12456L, "ext4"));

        // Assert
        sample.CgroupMeasured.ShouldBeFalse();
        sample.CgroupHeadroomBytes.ShouldBeNull();
        sample.HostAvailableBytes.ShouldBe(204800L);
        sample.ShmemBytes.ShouldBe(7168L);
        sample.ScratchAvailableBytes.ShouldBe(12456L);
    }

    [Fact]
    public void Reports_finite_headroom_for_a_capped_visible_root()
    {
        // Arrange
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["/proc/self/cgroup"] = "0::/\n",
            ["/proc/self/mountinfo"] = UnifiedRootMount,
            ["/sys/fs/cgroup/memory.max"] = "5000\n",
            ["/sys/fs/cgroup/memory.current"] = "3750\n",
        };

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(path => files.GetValueOrDefault(path), static () => (2048L, "ext4"));

        // Assert
        sample.CgroupMeasured.ShouldBeTrue();
        sample.CgroupHeadroomBytes.ShouldBe(1250L);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reports_cgroup_unknown_when_the_kernel_read_fails(bool denied)
    {
        // Arrange
        Exception failure = denied ? new UnauthorizedAccessException("fixture denial") : new IOException("fixture read failure");

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(
            path => path == "/proc/self/cgroup" ? throw failure : null, static () => (1234L, "ext4"));

        // Assert
        sample.CgroupMeasured.ShouldBeFalse();
        sample.CgroupHeadroomBytes.ShouldBeNull();
        sample.ScratchAvailableBytes.ShouldBe(1234L);
    }

    [Theory]
    [InlineData("MemAvailable: 200 MB", "some total=18")]
    [InlineData("MemAvailable: invalid kB", "some avg10=invalid")]
    [InlineData("MemAvailable: 9223372036854775807 kB", "some avg10=100.01")]
    public void Malformed_linux_metrics_are_unknown_without_discarding_valid_shared_memory(string memAvailable, string pressure)
    {
        // Arrange
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["/proc/meminfo"] = memAvailable + "\nShmem: 7 kB\n",
            ["/proc/pressure/memory"] = pressure,
        };

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(path => files.GetValueOrDefault(path), static () => (1234L, "ext4"));

        // Assert
        sample.HostAvailableBytes.ShouldBeNull();
        sample.MemoryPressurePercent.ShouldBeNull();
        sample.ShmemBytes.ShouldBe(7168L);
    }

    [Fact(SkipType = typeof(ResourceBudgetCommandTests), SkipUnless = nameof(ResourceBudgetCommandTests.Linux),
        Skip = "The filesystem reading requires Linux.")]
    public void Missing_scratch_directory_reports_an_unavailable_filesystem()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var missing = Path.Combine(directory.Path, "missing-child");

        // Act
        var sample = LinuxResourceBudgetSampler.Measure(missing);

        // Assert
        sample.ScratchAvailableBytes.ShouldBeNull();
        sample.ScratchFilesystem.ShouldBeNull();
    }
}
