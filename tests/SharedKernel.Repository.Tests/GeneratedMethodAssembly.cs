using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace PandaBear.Repository.Tests;

/// <summary>Builds the metadata-only assembly that sits beside a test symbol file, with one method that the compiler may have generated.</summary>
internal static class GeneratedMethodAssembly
{
    private const ushort PortablePdbVersion = 0x0100;

    /// <summary>Returns an assembly with one method and a debug directory that names <paramref name="pdbId"/>; the method carries <c>CompilerGeneratedAttribute</c> when <paramref name="generated"/> is set.</summary>
    public static byte[] Build(BlobContentId pdbId, bool generated)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Core.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("Core"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        var runtime = metadata.AddAssemblyReference(metadata.GetOrAddString("System.Runtime"), new Version(10, 0, 0, 0), default, default, 0, default);
        var attribute = metadata.AddTypeReference(
            runtime, metadata.GetOrAddString("System.Runtime.CompilerServices"), metadata.GetOrAddString("CompilerGeneratedAttribute"));
        var constructor = metadata.AddMemberReference(
            attribute, metadata.GetOrAddString(".ctor"), metadata.GetOrAddBlob(new byte[] { 0x20, 0x00, 0x01 }));
        var method = MetadataTokens.MethodDefinitionHandle(1);
        metadata.AddTypeDefinition(0, default, metadata.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), method);
        metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Sealed, default, metadata.GetOrAddString("Core"), default, MetadataTokens.FieldDefinitionHandle(1), method);
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.Runtime, metadata.GetOrAddString("Run"),
            metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 }), -1, MetadataTokens.ParameterHandle(1));
        if (generated)
        {
            metadata.AddCustomAttribute(method, constructor, metadata.GetOrAddBlob(new byte[] { 0x01, 0x00 }));
        }

        var debug = new DebugDirectoryBuilder();
        debug.AddCodeViewEntry("Core.pdb", pdbId, PortablePdbVersion);
        var image = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(), debugDirectoryBuilder: debug);
        var output = new BlobBuilder();
        image.Serialize(output);
        return output.ToArray();
    }
}
