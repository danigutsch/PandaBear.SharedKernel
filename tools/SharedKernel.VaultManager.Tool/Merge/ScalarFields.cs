using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Every single-valued text field, in report order.</summary>
internal static class ScalarFields
{
    /// <summary>
    /// Fields in report order: notes, login, card, identity, then SSH key fields. Passwords compare exactly, user names
    /// and authenticator keys by their <see cref="ItemNormalizer"/> keys, notes and keys by line-ending-normalized text,
    /// and other values trimmed.
    /// </summary>
    public static IReadOnlyList<ScalarField> All
    {
        get;
    } =
    [
        new(FieldPaths.Notes, "Notes", null, item => item.Notes, (item, value) => item with { Notes = value }, ItemNormalizer.Notes),
        Login(FieldPaths.LoginUsername, "Username", login => login.Username, (login, value) => login with { Username = value }, ItemNormalizer.Username),
        Login(FieldPaths.LoginPassword, "Password", login => login.Password, (login, value) => login with { Password = value }, Exact),
        Login(FieldPaths.LoginTotp, "TOTP", login => login.Totp, (login, value) => login with { Totp = value }, ItemNormalizer.Totp),
        Card("cardholderName", "Cardholder name", card => card.CardholderName, (card, value) => card with { CardholderName = value }),
        Card("brand", "Brand", card => card.Brand, (card, value) => card with { Brand = value }),
        Card("number", "Card number", card => card.Number, (card, value) => card with { Number = value }),
        Card("expMonth", "Expiration month", card => card.ExpMonth, (card, value) => card with { ExpMonth = value }),
        Card("expYear", "Expiration year", card => card.ExpYear, (card, value) => card with { ExpYear = value }),
        Card("code", "Security code", card => card.Code, (card, value) => card with { Code = value }),
        Identity("title", "Title", identity => identity.Title, (identity, value) => identity with { Title = value }),
        Identity("firstName", "First name", identity => identity.FirstName, (identity, value) => identity with { FirstName = value }),
        Identity("middleName", "Middle name", identity => identity.MiddleName, (identity, value) => identity with { MiddleName = value }),
        Identity("lastName", "Last name", identity => identity.LastName, (identity, value) => identity with { LastName = value }),
        Identity("address1", "Address 1", identity => identity.Address1, (identity, value) => identity with { Address1 = value }),
        Identity("address2", "Address 2", identity => identity.Address2, (identity, value) => identity with { Address2 = value }),
        Identity("address3", "Address 3", identity => identity.Address3, (identity, value) => identity with { Address3 = value }),
        Identity("city", "City", identity => identity.City, (identity, value) => identity with { City = value }),
        Identity("state", "State", identity => identity.State, (identity, value) => identity with { State = value }),
        Identity("postalCode", "Postal code", identity => identity.PostalCode, (identity, value) => identity with { PostalCode = value }),
        Identity("country", "Country", identity => identity.Country, (identity, value) => identity with { Country = value }),
        Identity("company", "Company", identity => identity.Company, (identity, value) => identity with { Company = value }),
        Identity("email", "Email", identity => identity.Email, (identity, value) => identity with { Email = value }),
        Identity("phone", "Phone", identity => identity.Phone, (identity, value) => identity with { Phone = value }),
        Identity("ssn", "Social security number", identity => identity.Ssn, (identity, value) => identity with { Ssn = value }),
        Identity("username", "Identity username", identity => identity.Username, (identity, value) => identity with { Username = value }),
        Identity("passportNumber", "Passport number", identity => identity.PassportNumber, (identity, value) => identity with { PassportNumber = value }),
        Identity("licenseNumber", "License number", identity => identity.LicenseNumber, (identity, value) => identity with { LicenseNumber = value }),
        SshKey("privateKey", "Private key", key => key.PrivateKey, (key, value) => key with { PrivateKey = value }, ItemNormalizer.Notes),
        SshKey("publicKey", "Public key", key => key.PublicKey, (key, value) => key with { PublicKey = value }, ItemNormalizer.Notes),
        SshKey("keyFingerprint", "Key fingerprint", key => key.KeyFingerprint, (key, value) => key with { KeyFingerprint = value }, Trimmed),
    ];

    private static string Exact(string value) => value;

    private static string Trimmed(string value) => value.Trim();

    private static ScalarField Login(string path, string label, Func<LoginData, string?> get, Func<LoginData, string?, LoginData> set, Func<string, string> key) =>
        new(path, label, VaultItemType.Login, item => item.Login is { } login ? get(login) : null, (item, value) => item with { Login = set(item.Login ?? new LoginData(), value) }, key);

    private static ScalarField Card(string property, string label, Func<CardData, string?> get, Func<CardData, string?, CardData> set) =>
        new(FieldPaths.CardPrefix + property, label, VaultItemType.Card, item => item.Card is { } card ? get(card) : null, (item, value) => item with { Card = set(item.Card ?? new CardData(), value) }, Trimmed);

    private static ScalarField Identity(string property, string label, Func<IdentityData, string?> get, Func<IdentityData, string?, IdentityData> set) =>
        new(FieldPaths.IdentityPrefix + property, label, VaultItemType.Identity, item => item.Identity is { } identity ? get(identity) : null, (item, value) => item with { Identity = set(item.Identity ?? new IdentityData(), value) }, Trimmed);

    private static ScalarField SshKey(string property, string label, Func<SshKeyData, string?> get, Func<SshKeyData, string?, SshKeyData> set, Func<string, string> key) =>
        new(FieldPaths.SshKeyPrefix + property, label, VaultItemType.SshKey, item => item.SshKey is { } sshKey ? get(sshKey) : null, (item, value) => item with { SshKey = set(item.SshKey ?? new SshKeyData(), value) }, key);
}
