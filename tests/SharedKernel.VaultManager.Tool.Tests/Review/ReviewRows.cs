using PandaBear.VaultManager.Tool.Review;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Finds where a field or a finding sits in the tables of the review window.</summary>
internal static class ReviewRows
{
    /// <summary>Row of a field in the field table of a group.</summary>
    public static int Field(ReviewSession session, string groupKey, string fieldPath)
    {
        IReadOnlyList<ReviewField> fields = session.Fields(groupKey);
        for (int index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].FieldPath, fieldPath, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Group {groupKey} shows no field {fieldPath}.");
    }

    /// <summary>Row of a finding in the findings table of a group.</summary>
    public static int Finding(ReviewSession session, string groupKey, string findingId)
    {
        IReadOnlyList<ReviewFinding> findings = session.Findings(groupKey);
        for (int index = 0; index < findings.Count; index++)
        {
            if (string.Equals(findings[index].FindingId, findingId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Group {groupKey} shows no finding {findingId}.");
    }
}
