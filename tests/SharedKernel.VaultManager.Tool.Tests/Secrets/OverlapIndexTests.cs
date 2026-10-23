using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class OverlapIndexTests
{
    [Fact]
    public void FindOverlap_agrees_with_comparing_every_kept_location_across_blocks_fields_and_empty_locations()
    {
        SecretLocation[] candidates = [.. OverlapSamples.Locations(count: 3000, maxStart: 6000, maxLength: 700)];
        var index = new OverlapIndex();
        var kept = new List<SecretLocation>();
        var expected = new List<bool>();
        var actual = new List<bool>();
        var reported = new List<SecretLocation>();

        foreach (SecretLocation candidate in candidates)
        {
            int position = index.FindOverlap(candidate);
            expected.Add(kept.Any(location => OverlapIndex.Overlaps(location, candidate)));
            actual.Add(position >= 0);
            if (position >= 0)
            {
                reported.Add(kept[position]);
                continue;
            }

            index.Add(candidate);
            kept.Add(candidate);
        }

        actual.ShouldBe(expected);
        reported.Count.ShouldBeGreaterThan(0);
        kept.Count.ShouldBeGreaterThan(100);
        reported.Zip(candidates.Where((_, number) => actual[number])).ShouldAllSatisfy(pair => OverlapIndex.Overlaps(pair.First, pair.Second).ShouldBeTrue());
    }

    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(5, 0, false)]
    [InlineData(260, 0, false)]
    [InlineData(250, 5, true)]
    [InlineData(260, 1, false)]
    [InlineData(4, 2, true)]
    public void FindOverlap_matches_locations_that_share_text_and_empty_locations_strictly_inside(int start, int length, bool overlaps)
    {
        var index = new OverlapIndex();
        index.Add(new SecretLocation("attachment:notes.txt", 5, 255));

        int position = index.FindOverlap(new SecretLocation("attachment:notes.txt", start, length));

        bool found = position >= 0;
        found.ShouldBe(overlaps);
    }
}
