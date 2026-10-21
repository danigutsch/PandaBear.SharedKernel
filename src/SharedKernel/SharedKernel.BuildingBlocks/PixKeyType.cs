namespace PandaBear.BuildingBlocks;

/// <summary>The explicitly selected BCB DICT addressing-key format.</summary>
public enum PixKeyType
{
    /// <summary>No format selected; rejected by the factory.</summary>
    None = 0,
    /// <summary>A Brazilian individual taxpayer identifier.</summary>
    Cpf = 1,
    /// <summary>A Brazilian company taxpayer identifier.</summary>
    Cnpj = 2,
    /// <summary>An international telephone number in canonical E.164 form.</summary>
    Phone = 3,
    /// <summary>A lowercase email address in the DICT format.</summary>
    Email = 4,
    /// <summary>A lowercase UUID in the DICT EVP format.</summary>
    Evp = 5
}
