namespace PandaBear.Blazor.Site;

/// <summary>A column width of a <see cref="CardGrid" />: the smallest width of its columns, or the largest.</summary>
public enum CardColumnWidth
{
    /// <summary>16rem (256px), the default smallest width: four columns in the default 72rem content width.</summary>
    Narrow = 0,

    /// <summary>22rem (352px): three columns in the default content width.</summary>
    Medium = 1,

    /// <summary>32rem (512px): two columns in the default content width.</summary>
    Wide = 2,
}
