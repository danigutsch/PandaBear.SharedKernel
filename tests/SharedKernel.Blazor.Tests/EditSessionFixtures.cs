namespace PandaBear.Blazor.Tests;

/// <summary>Builds <see cref="EditSession{TKey, TForm, TRecord}"/> instances and callbacks for tests.</summary>
internal static class EditSessionFixtures
{
    /// <summary>Creates a session over the name field with the prefix and noun <c>item</c>.</summary>
    /// <returns>The session; the caller disposes it.</returns>
    public static EditSession<Guid, EditSessionForm, EditSessionRecord> Create() => new(["Name"], "item", "item");

    /// <summary>Copies the name of the record into the form.</summary>
    /// <param name="form">The form.</param>
    /// <param name="record">The record.</param>
    public static void Fill(EditSessionForm form, EditSessionRecord record) => form.Name = record.Name;
}
