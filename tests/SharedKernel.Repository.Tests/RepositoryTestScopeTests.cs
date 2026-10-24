namespace PandaBear.Repository.Tests;

public sealed class RepositoryTestScopeTests
{
    [Fact]
    public void Dispose_removes_read_only_files_such_as_git_objects()
    {
        // Arrange
        var scope = new RepositoryTestScope();
        var objectPath = Path.Combine(scope.Root, ".git", "objects", "ab", "cdef");
        scope.WriteFile(".git/objects/ab/cdef", "object");
        File.SetAttributes(objectPath, File.GetAttributes(objectPath) | FileAttributes.ReadOnly);

        // Act
        scope.Dispose();

        // Assert
        Directory.Exists(scope.Root).ShouldBeFalse("Git writes object files read-only, and Windows refuses to delete them otherwise.");
    }
}
