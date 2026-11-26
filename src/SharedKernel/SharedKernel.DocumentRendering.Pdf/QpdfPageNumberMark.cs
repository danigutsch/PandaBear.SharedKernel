using System.Globalization;
using System.Text;

namespace SharedKernel.DocumentRendering.Pdf;

/// <summary>Writes isolated PDF vector digits in the reserved pagination band.</summary>
internal static class QpdfPageNumberMark
{
    private const int FooterY = 16;
    private const int DigitWidth = 6;
    private const int DigitAdvance = 8;

    // These vector marks need no resources from the document.
    private static readonly string[] Digits =
    [
        "3 10 m 1 10 0 8 0 5 c 0 2 1 0 3 0 c 5 0 6 2 6 5 c 6 8 5 10 3 10 c h",
        "1 8 m 3 10 l 3 0 l 1 0 m 5 0 l",
        "0 8 m 1 11 6 11 6 8 c 6 6 2 4 0 0 c 6 0 l",
        "0 9 m 2 11 6 10 6 8 c 6 6 4 5 2 5 c 4 5 6 4 6 2 c 6 0 2 -1 0 1 c",
        "5 0 m 5 10 l 0 3 l 6 3 l",
        "6 10 m 1 10 l 0 5 l 2 7 6 6 6 3 c 6 0 2 -1 0 1 c",
        "6 9 m 4 11 0 10 0 4 c 0 1 1 0 3 0 c 7 0 7 6 3 6 c 2 6 1 5 0 4 c",
        "0 10 m 6 10 l 2 0 l",
        "3 5 m 7 5 7 0 3 0 c -1 0 -1 5 3 5 c 7 5 7 10 3 10 c -1 10 -1 5 3 5 c",
        "0 1 m 2 -1 6 0 6 6 c 6 9 5 10 3 10 c -1 10 -1 4 3 4 c 4 4 5 5 6 6 c",
    ];

    /// <summary>Centers a sequential decimal number below the page body using its width in PDF points.</summary>
    public static string Write(int pageNumber, decimal pageWidth)
    {
        var number = pageNumber.ToString(CultureInfo.InvariantCulture);
        var width = (number.Length - 1) * DigitAdvance + DigitWidth;
        var x = (pageWidth - width) / 2;
        var builder = new StringBuilder();
        builder.Append("q\n/Artifact << /Type /Pagination /Attached [/Bottom] >> BDC\n");
        builder.Append("0 G 0.85 w 1 J 1 j\n");
        builder.Append(CultureInfo.InvariantCulture, $"1 0 0 1 {x:0.###} {FooterY} cm\n");
        foreach (var digit in number)
        {
            builder.Append(Digits[digit - '0']).Append(" S\n");
            builder.Append(CultureInfo.InvariantCulture, $"1 0 0 1 {DigitAdvance} 0 cm\n");
        }

        return builder.Append("EMC\nQ\n").ToString();
    }
}
