using System.Buffers.Binary;
using SharedKernel.Testing;

namespace SharedKernel.UploadSanitizing.Tests.Fonts;

/// <summary>
/// Fuzz-style tests of <see cref="FontSanitizer"/>: truncations, single-byte mutations, lengths that overflow 32 bits and random bytes
/// never throw, a declared length never sizes an allocation, and every success is the input unchanged.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class FontSanitizerRobustnessTests
{
    private static readonly byte[] MutationValues = [0x00, 0x01, 0x3F, 0x7F, 0x80, 0xC0, 0xFF];

    /// <summary>Gets sound fonts of every directory and block shape.</summary>
    public static TheoryData<string, byte[]> Samples => new()
    {
        { "one table", Woff2Samples.Font() },
        { "transformed glyf and loca", Woff2Samples.Font(directory: Woff2Samples.TransformedGlyfAndLoca, tables: 2) },
        { "null-transformed glyf and loca", Woff2Samples.Font(directory: Woff2Samples.UntransformedGlyfAndLoca, tables: 2) },
        { "an arbitrary tag", Woff2Samples.Font(directory: Woff2Samples.ArbitraryTag) },
        { "metadata and private blocks", Woff2Samples.FontWithMetadataAndPrivateData() },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_truncation_of_a_sound_font_is_refused(string name, byte[] file)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(file);
        FontSanitizer.Sanitize(file, FontSanitizerOptions.Default).IsSuccess.ShouldBeTrue(name);

        // Act
        for (var length = 0; length < file.Length; length++)
        {
            var prefix = file.AsSpan(0, length);
            var result = FontSanitizer.Sanitize(prefix, FontSanitizerOptions.Default);
            var inPlace = FontSanitizer.SanitizeInPlace(prefix.ToArray(), FontSanitizerOptions.Default);

            // Assert
            var expected = length < 48 ? FontSanitizer.NotAFontMessage : FontSanitizer.MalformedMessage;
            result.ShouldBeRejected(expected);
            inPlace.ShouldBeRejected(expected);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_single_byte_change_of_a_sound_font_is_answered_with_a_result(string name, byte[] file)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(file);
        var answered = 0;

        // Act
        for (var index = 0; index < file.Length; index++)
        {
            foreach (var value in MutationValues)
            {
                var mutated = file.ToArray();
                mutated[index] = value;
                FontSanitizer.Sanitize(mutated, FontSanitizerOptions.Default).ShouldBeSuccessOrRejected(mutated);
                answered++;
            }
        }

        // Assert
        answered.ShouldBe(file.Length * MutationValues.Length, name);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_change_of_the_signature_or_the_declared_length_is_always_refused(string name, byte[] file)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(file);

        // Act
        for (var index = 0; index < 12; index++)
        {
            if (index is >= 4 and < 8)
            {
                continue; // The flavour: other accepted flavours exist.
            }

            foreach (var value in MutationValues)
            {
                if (value == file[index])
                {
                    continue;
                }

                var mutated = file.ToArray();
                mutated[index] = value;

                // Assert
                FontSanitizer.Sanitize(mutated, FontSanitizerOptions.Default).IsFailure.ShouldBeTrue($"{name}: byte {index} = {value}");
            }
        }
    }

    /// <summary>Gets fonts whose declared offsets and lengths wrap around when added in 32 bits.</summary>
    public static TheoryData<string, byte[]> Overflowing => new()
    {
        // 50 + 0xFFFFFFCE wraps to 0 in 32 bits, which a wrapping sum would accept as the end of the data.
        { "a compressed size that wraps", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.CompressedSizeOffset, 0xFFFFFFCE)) },
        { "the largest compressed size", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.CompressedSizeOffset, uint.MaxValue)) },
        { "a metadata length that wraps", Woff2Samples.WithBlock(Woff2Samples.MetaOffset, 60, 0xFFFFFFC8) },
        { "the largest metadata offset and length", Woff2Samples.WithBlock(Woff2Samples.MetaOffset, uint.MaxValue - 3, uint.MaxValue) },
        { "a private length that wraps", Woff2Samples.WithBlock(Woff2Samples.PrivateOffset, 64, 0xFFFFFFC4) },
        { "the largest private offset and length", Woff2Samples.WithBlock(Woff2Samples.PrivateOffset, uint.MaxValue - 3, uint.MaxValue) },
        { "the largest table count", Woff2Samples.Font(tables: ushort.MaxValue) },
        { "a declared length of 2^32 - 1", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.LengthOffset, uint.MaxValue)) },
    };

    [Theory]
    [MemberData(nameof(Overflowing))]
    public void A_length_that_overflows_32_bits_is_malformed(string kind, byte[] file)
    {
        // Act
        var result = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);

        // Assert
        result.IsFailure.ShouldBeTrue(kind);
        result.ShouldBeRejected(FontSanitizer.MalformedMessage);
    }

    [Fact]
    public void Declared_lengths_never_size_an_allocation()
    {
        // Arrange
        var file = Woff2Samples.Font(tables: ushort.MaxValue, patch: bytes =>
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), uint.MaxValue);
            Woff2Samples.Write(bytes, Woff2Samples.CompressedSizeOffset, uint.MaxValue);
            Woff2Samples.Write(bytes, Woff2Samples.MetaOffset + 4, uint.MaxValue);
            Woff2Samples.Write(bytes, Woff2Samples.MetaOffset + 8, uint.MaxValue);
            Woff2Samples.Write(bytes, Woff2Samples.PrivateOffset + 4, uint.MaxValue);
        });
        FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);
        FontSanitizer.SanitizeInPlace(file, FontSanitizerOptions.Default);
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var copied = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);
        var inPlace = FontSanitizer.SanitizeInPlace(file, FontSanitizerOptions.Default);

        // Assert
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        copied.ShouldBeRejected(FontSanitizer.MalformedMessage);
        inPlace.ShouldBeRejected(FontSanitizer.MalformedMessage);
        allocated.ShouldBeLessThan(16 * 1024);
    }

    [Fact]
    public void Random_bytes_behind_a_sound_header_are_answered_with_a_result()
    {
        // Arrange
        var random = new Random(20261111);
        var answered = 0;

        // Act
        for (var iteration = 0; iteration < 5000; iteration++)
        {
            var file = new byte[48 + random.Next(0, 200)];
            random.NextBytes(file);
            var span = file.AsSpan();
            BinaryPrimitives.WriteUInt32BigEndian(span, Woff2Samples.Signature);
            BinaryPrimitives.WriteUInt32BigEndian(span[4..], Woff2Samples.TrueType);
            BinaryPrimitives.WriteUInt32BigEndian(span[Woff2Samples.LengthOffset..], (uint)file.Length);
            BinaryPrimitives.WriteUInt16BigEndian(span[12..], (ushort)random.Next(1, 8));
            BinaryPrimitives.WriteUInt16BigEndian(span[14..], 0);
            Woff2Samples.Write(file, Woff2Samples.CompressedSizeOffset, (uint)random.Next(1, 64));
            if (random.Next(2) == 0)
            {
                span[28..48].Clear();
            }

            FontSanitizer.Sanitize(file, FontSanitizerOptions.Default).ShouldBeSuccessOrRejected(file);
            answered++;
        }

        // Assert
        answered.ShouldBe(5000);
    }

    [Fact]
    public void Random_bytes_behind_the_signature_are_answered_with_a_result()
    {
        // Arrange
        var random = new Random(20261112);
        var answered = 0;

        // Act
        for (var iteration = 0; iteration < 5000; iteration++)
        {
            var file = new byte[4 + random.Next(0, 300)];
            random.NextBytes(file);
            "wOF2"u8.CopyTo(file);
            FontSanitizer.SanitizeInPlace(file.ToArray(), FontSanitizerOptions.Default).ShouldBeSuccessOrRejected(file);
            answered++;
        }

        // Assert
        answered.ShouldBe(5000);
    }
}
