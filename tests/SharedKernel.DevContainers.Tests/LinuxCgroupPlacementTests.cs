namespace SharedKernel.DevContainers.Tests;

/// <summary>Verifies cgroup and process identity readings from synthetic Linux files.</summary>
public sealed class LinuxCgroupPlacementTests
{
    private const string Parent = "/user.slice/owned.slice";
    private const string Launcher = Parent + "/launcher.scope";
    private const string Payload = Parent + "/libpod.scope";
    private const string Membership = "0::" + Payload + "\n";
    private const string ParentDirectory = "/sys/fs/cgroup" + Parent;
    private const string UserSliceDirectory = "/sys/fs/cgroup/user.slice";
    private const string PayloadDirectory = "/sys/fs/cgroup" + Payload;
    private const string UnifiedRootMount = "28 22 0:25 / /sys/fs/cgroup rw - cgroup2 cgroup rw\n";

    // The zeroes cover stat fields 4 through 21. Field 22 follows this prefix.
    private const string ProcessStatCommand = "412 (private) workload) ";
    private const string ProcessStatFields = " 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ";
    private const string ProcessStatPrefix = ProcessStatCommand + "S" + ProcessStatFields;

    [Theory]
    [InlineData(UnifiedRootMount, true)]
    [InlineData("28 22 0:25 /user.slice /sys/fs/cgroup rw - cgroup2 cgroup rw\n", false)]
    [InlineData("28 22 0:25 / /sys/fs/cgroup rw - tmpfs tmpfs rw\n", false)]
    [InlineData(null, false)]
    public void Recognizes_only_a_visible_unified_root_mount(string? mountInfo, bool expected)
    {
        // Arrange
        var mount = mountInfo;

        // Act
        var result = LinuxCgroupPlacement.HasUnifiedRootMount(mount);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(Launcher, true)]
    [InlineData(Payload, true)]
    [InlineData(Payload + "/child", true)]
    [InlineData(Parent, false)]
    [InlineData(Parent + "-other/launcher.scope", false)]
    [InlineData(Parent + "//launcher.scope", false)]
    [InlineData(Parent + "/./launcher.scope", false)]
    [InlineData(Parent + "/../other.scope", false)]
    [InlineData("user.slice/owned.slice/launcher.scope", false)]
    public void Recognizes_a_strict_descendant_by_complete_path_components(string member, bool isDescendant)
    {
        // Arrange
        var parent = Parent;

        // Act
        var result = LinuxCgroupPlacement.IsStrictDescendant(member, parent);

        // Assert
        result.ShouldBe(isDescendant);
    }

    [Theory]
    [InlineData("16000\n", "0\n", 12000L, 0L)]
    [InlineData("8000\n", "1024\n", 8000L, 512L)]
    [InlineData("max\n", "0\n", null, null)]
    [InlineData("16000\n", "max\n", null, null)]
    [InlineData("16000\n", null, null, null)]
    [InlineData(null, "0\n", null, null)]
    [InlineData("0\n", "0\n", null, null)]
    [InlineData("16000\n", "-1\n", null, null)]
    public void Requires_finite_explicit_parent_caps_and_preserves_tighter_ancestor_caps(
        string? memory, string? swap, long? expectedMemory, long? expectedSwap)
    {
        // Arrange
        Dictionary<string, string?> files = new(StringComparer.Ordinal)
        {
            [ParentDirectory + "/memory.max"] = memory,
            [ParentDirectory + "/memory.swap.max"] = swap,
            [UserSliceDirectory + "/memory.max"] = "12000\n",
            [UserSliceDirectory + "/memory.swap.max"] = "512\n",
            [ParentDirectory + "/launcher.scope/memory.max"] = "4000\n",
            [ParentDirectory + "/launcher.scope/memory.swap.max"] = "0\n",
        };

        // Act
        var limits = LinuxCgroupPlacement.ReadParentLimits(Parent, Launcher, path => files.GetValueOrDefault(path));

        // Assert
        var actualMemory = limits?.MemoryMaximumBytes;
        var actualSwap = limits?.SwapMaximumBytes;
        actualMemory.ShouldBe(expectedMemory);
        actualSwap.ShouldBe(expectedSwap);
    }

    [Theory]
    [InlineData(Payload, "max\n", "max\n", 12000L, 512L)]
    [InlineData(Payload, "6000\n", "0\n", 6000L, 0L)]
    [InlineData(Payload, null, "0\n", null, null)]
    [InlineData(Parent + "/../libpod.scope", "6000\n", "0\n", null, null)]
    public void Reads_effective_member_caps_from_the_member_and_its_ancestors(
        string member, string? memory, string? swap, long? expectedMemory, long? expectedSwap)
    {
        // Arrange
        Dictionary<string, string?> files = new(StringComparer.Ordinal)
        {
            [PayloadDirectory + "/memory.max"] = memory,
            [PayloadDirectory + "/memory.swap.max"] = swap,
            [ParentDirectory + "/memory.max"] = "16000\n",
            [ParentDirectory + "/memory.swap.max"] = "1024\n",
            [UserSliceDirectory + "/memory.max"] = "12000\n",
            [UserSliceDirectory + "/memory.swap.max"] = "512\n",
        };

        // Act
        var limits = LinuxCgroupPlacement.ReadEffectiveLimits(member, path => files.GetValueOrDefault(path));

        // Assert
        var actualMemory = limits?.MemoryMaximumBytes;
        var actualSwap = limits?.SwapMaximumBytes;
        actualMemory.ShouldBe(expectedMemory);
        actualSwap.ShouldBe(expectedSwap);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Effective_caps_are_unavailable_when_the_kernel_read_fails(bool permissionDenied)
    {
        // Arrange
        Exception readFailure = permissionDenied ? new UnauthorizedAccessException("memory cap unreadable") : new IOException("memory cap unavailable");

        // Act
        var limits = LinuxCgroupPlacement.ReadEffectiveLimits(Payload, _ => throw readFailure);

        // Assert
        limits.ShouldBeNull();
    }

    [Theory]
    [InlineData(Parent)]
    [InlineData(Parent + "-other/launcher.scope")]
    [InlineData(Parent + "/../other.scope")]
    public void Rejects_a_launcher_outside_the_parent_before_reading_limits(string launcher)
    {
        // Arrange
        const string UnexpectedRead = "Unrelated launchers must be rejected before cgroup file reads.";

        // Act
        var limits = LinuxCgroupPlacement.ReadParentLimits(
            Parent, launcher, static _ => throw new InvalidOperationException(UnexpectedRead));

        // Assert
        limits.ShouldBeNull();
    }

    [Theory]
    [InlineData("81", Membership, Payload)]
    [InlineData("82", Membership, null)]
    [InlineData(null, Membership, null)]
    [InlineData("invalid", Membership, null)]
    [InlineData("81", null, null)]
    [InlineData("81", "0::" + Parent + "/../other.scope\n", null)]
    [InlineData("81", "0::" + Parent + "//libpod.scope\n", null)]
    [InlineData("81", Membership + "2:memory:/legacy.scope\n", null)]
    [InlineData("81", Membership + "not-a-membership-record\n", null)]
    [InlineData("81", Membership + "0::" + Launcher + "\n", null)]
    [InlineData("81", "2:cpu:/legacy.scope\n" + Membership, Payload)]
    public void Associates_membership_with_one_live_process_birth(
        string? secondBirth, string? membership, string? expected)
    {
        // Arrange
        const string ProcRoot = "/proc-fixture";
        const int ProcessId = 412;
        Queue<string?> stat = new(
            [ProcessStatPrefix + "81 0\n", secondBirth is null ? null : ProcessStatPrefix + secondBirth + " 0\n"]);

        // Act
        var result = LinuxCgroupPlacement.ReadStableProcessCgroup(ProcRoot, ProcessId, path => path switch
        {
            ProcRoot + "/412/stat" => stat.Dequeue(),
            ProcRoot + "/412/cgroup" => membership,
            _ => null,
        });

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("412 invalid")]
    [InlineData("412 (payload)")]
    [InlineData("412 (payload) S 0")]
    public void An_incomplete_process_stat_cannot_bind_membership_to_a_birth(string stat)
    {
        // Arrange
        const string ProcRoot = "/proc-fixture";
        const int ProcessId = 412;

        // Act
        var result = LinuxCgroupPlacement.ReadStableProcessCgroup(ProcRoot, ProcessId, path => path switch
        {
            ProcRoot + "/412/stat" => stat,
            ProcRoot + "/412/cgroup" => Membership,
            _ => null,
        });

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData('Z', 'Z')]
    [InlineData('X', 'X')]
    [InlineData('S', 'Z')]
    [InlineData('S', 'X')]
    public void Rejects_dead_process_membership_even_when_birth_is_stable(char firstState, char lastState)
    {
        // Arrange
        const string ProcRoot = "/proc-fixture";
        const int ProcessId = 412;
        Queue<string> stat = new(
        [
            ProcessStatCommand + firstState + ProcessStatFields + "81 0\n",
            ProcessStatCommand + lastState + ProcessStatFields + "81 0\n",
        ]);

        // Act
        var result = LinuxCgroupPlacement.ReadStableProcessCgroup(ProcRoot, ProcessId, path => path switch
        {
            ProcRoot + "/412/stat" => stat.Dequeue(),
            ProcRoot + "/412/cgroup" => Membership,
            _ => null,
        });

        // Assert
        result.ShouldBeNull();
    }
}
