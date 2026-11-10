using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Reads Release portable PDBs to tell a source file without executable code from one that the tests missed.</summary>
internal static class PortableSymbols
{
    private static readonly string[] AssemblyExtensions = [".dll", ".exe"];

    private static readonly Guid Sha256Algorithm = new("8829d00f-11b8-4213-878b-770e8597ac16");

    /// <summary>
    /// Reports whether the symbols name <paramref name="path"/> with the SHA-256 hash of its current content and map no
    /// executable code to it, as for a file of interfaces, enums, or attributes only. A sequence point of a method that carries
    /// <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/> is not executable code of the file, as for the
    /// copy constructor that the compiler adds to a record without members, because the coverage tools leave such a method out
    /// of their reports. Only an assembly beside the symbol file whose debug directory names that file can mark a method.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="path">The repository-relative source file.</param>
    /// <param name="symbolFiles">The repository-relative portable PDBs that the verified symbol evidence names.</param>
    /// <returns>
    /// <see langword="false"/> when no PDB names the file, a PDB records another hash or hash algorithm for it, or a PDB
    /// maps a sequence point to it.
    /// </returns>
    /// <exception cref="InvalidDataException">A file that starts like a portable PDB is not a valid one.</exception>
    public static bool HasOnlyNonExecutableCode(string root, string path, IEnumerable<string> symbolFiles)
    {
        var checksum = SHA256.HashData(File.ReadAllBytes(RepositoryPath.Resolve(root, path)));
        var found = false;
        var signature = new byte[4];
        foreach (var file in symbolFiles)
        {
            using var stream = File.OpenRead(RepositoryPath.Resolve(root, file));

            // Windows PDBs carry no portable document table, so they are not evidence.
            if (stream.ReadAtLeast(signature, 4, throwOnEndOfStream: false) != 4 || !signature.AsSpan().SequenceEqual("BSJB"u8))
            {
                continue;
            }

            stream.Position = 0;
            try
            {
                using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
                var reader = provider.GetMetadataReader();
                var documents = new HashSet<DocumentHandle>();
                foreach (var handle in reader.Documents)
                {
                    var document = reader.GetDocument(handle);
                    var name = reader.GetString(document.Name).Replace('\\', '/');

                    // Deterministic builds map the source root to /_/.
                    if (CoberturaReport.Relative(name.StartsWith("/_/", StringComparison.Ordinal) ? name[3..] : name, root) != path)
                    {
                        continue;
                    }

                    if (reader.GetGuid(document.HashAlgorithm) != Sha256Algorithm || !reader.GetBlobBytes(document.Hash).AsSpan().SequenceEqual(checksum))
                    {
                        return false;
                    }

                    documents.Add(handle);
                    found = true;
                }

                if (documents.Count == 0)
                {
                    continue;
                }

                var generated = CompilerGeneratedMethods(RepositoryPath.Resolve(root, file), reader);
                foreach (var handle in reader.MethodDebugInformation)
                {
                    if (generated.Contains(MetadataTokens.GetRowNumber(handle)))
                    {
                        continue;
                    }

                    var method = reader.GetMethodDebugInformation(handle);
                    foreach (var point in method.GetSequencePoints())
                    {
                        if (!point.IsHidden && documents.Contains(point.Document.IsNil ? method.Document : point.Document))
                        {
                            return false;
                        }
                    }
                }
            }
            catch (BadImageFormatException exception)
            {
                throw new InvalidDataException(file + " is not a valid portable PDB.", exception);
            }
        }

        return found;
    }

    /// <summary>
    /// Returns the method rows that carry <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/> in the
    /// <c>.dll</c> or <c>.exe</c> beside <paramref name="symbolFile"/>, or none when that assembly is missing, unreadable, or
    /// records a different symbol file in its debug directory.
    /// </summary>
    private static HashSet<int> CompilerGeneratedMethods(string symbolFile, MetadataReader symbols)
    {
        var generated = new HashSet<int>();
        var assembly = AssemblyExtensions.Select(extension => Path.ChangeExtension(symbolFile, extension)).FirstOrDefault(File.Exists);
        if (assembly is null || symbols.DebugMetadataHeader is not { } header)
        {
            return generated;
        }

        try
        {
            using var stream = File.OpenRead(assembly);
            using var image = new PEReader(stream);
            if (!image.HasMetadata || !PairsWith(image, header.Id))
            {
                return generated;
            }

            var metadata = image.GetMetadataReader();
            foreach (var handle in metadata.MethodDefinitions)
            {
                foreach (var attribute in metadata.GetMethodDefinition(handle).GetCustomAttributes())
                {
                    if (IsCompilerGenerated(metadata, metadata.GetCustomAttribute(attribute)))
                    {
                        generated.Add(MetadataTokens.GetRowNumber(handle));
                    }
                }
            }
        }
        catch (BadImageFormatException)
        {
            // An assembly that cannot be read marks nothing, so every sequence point of the symbols still counts.
            generated.Clear();
        }

        return generated;
    }

    private static bool PairsWith(PEReader image, ImmutableArray<byte> symbolId)
    {
        foreach (var entry in image.ReadDebugDirectory())
        {
            if (entry.Type != DebugDirectoryEntryType.CodeView || !entry.IsPortableCodeView)
            {
                continue;
            }

            var id = image.ReadCodeViewDebugDirectoryData(entry).Guid.ToByteArray().Concat(BitConverter.GetBytes(entry.Stamp));
            if (id.SequenceEqual(symbolId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompilerGenerated(MetadataReader metadata, CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
        {
            return false;
        }

        var parent = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
        if (parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
        return metadata.StringComparer.Equals(type.Name, "CompilerGeneratedAttribute")
            && metadata.StringComparer.Equals(type.Namespace, "System.Runtime.CompilerServices");
    }
}
