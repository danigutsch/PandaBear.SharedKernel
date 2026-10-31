using PandaBear.BuildingBlocks;
using PandaBear.Results;
using PandaBear.ObjectStorage;
using PandaBear.Npgsql;

var bookingResult = CreateBooking("VT-42", "Ada Lovelace");
var bookingMessage = bookingResult.Match(
    static confirmation => $"Created booking: {confirmation}",
    static error => $"Booking failed: {error.Detail}");

const string tourCode = "VT-42";
var lookupMessage = FindTourSummary(tourCode).Match(
    static summary => $"Tour summary: {summary}",
    () => $"Tour '{tourCode}' was not found.");

var maybePassenger = Option.FromNullable(GetPassengerNickname("Ada Lovelace"));
var passengerMessage = maybePassenger.Match(
    static nickname => $"Nickname: {nickname}",
    static () => "Nickname: none");

var asyncLookupMessage = await FindTourSummaryAsync(tourCode)
    .Map(static summary => Task.FromResult(summary.ToUpperInvariant()))
    .Match(
        static summary => $"Async tour summary: {summary}",
        () => $"Tour '{tourCode}' was not found.");

var asyncPassengerOption = await ValueTask.FromResult(Option.FromNullable(GetPassengerNickname("Ada Lovelace")))
    .Map(static nickname => ValueTask.FromResult(nickname.ToUpperInvariant()));
var asyncPassengerMessage = await asyncPassengerOption.Match(
    static nickname => ValueTask.FromResult($"Async nickname: {nickname}"),
    static () => ValueTask.FromResult("Async nickname: none"));

var invalidBookingMessage = CreateBooking("", "").Match(
    static confirmation => $"Created booking: {confirmation}",
    static error => FormatValidationFailure(error));

var iban = Iban.Create("DE89370400440532013000");
var bic = Bic.Create("DEUTDEFF");
var cpf = Cpf.Create("01234567890");
var cnpj = Cnpj.Create("12ABC34501DE35");
var pix = PixKey.Create(PixKeyType.Cnpj, "12ABC34501DE35");
if (iban.IsFailure || bic.IsFailure || cpf.IsFailure || cnpj.IsFailure || pix.IsFailure
    || cpf.Value.Value != "01234567890" || pix.Value.Type != PixKeyType.Cnpj
    || !cnpj.Value.Equals(Cnpj.Create("12ABC34501DE35").Value)
    || iban.Value.ToString() != "Iban [redacted]"
    || Cpf.Create("11122233300").IsSuccess
    || PixKey.Create(PixKeyType.Email, "Pix@bcb.gov.br").IsSuccess)
{
    throw new InvalidOperationException("Payment identifier package checks failed.");
}

// These checks run against the packed packages, including during NativeAOT consumption.
var objectKey = ObjectStorageKey.From("media/café #1.jpg", 1024);
var prefix = ObjectStoragePrefix.From("media/");
var lockKey = PostgreSqlLockKey.FromIdentity("inventory:123");
if (objectKey != ObjectStorageKey.From("media/café #1.jpg")
    || objectKey == ObjectStorageKey.From("media/Café #1.jpg")
    || objectKey.ToEscapedPath() != "media/caf%C3%A9%20%231.jpg"
    || ObjectStorageKey.TryFrom("media/../secret", out _)
    || ObjectStorageKey.TryFrom("media/file\n.jpg", out _)
    || ObjectStoragePrefix.TryFrom("/", out _)
    || prefix.Value != "media/"
    || ObjectStoragePrefix.From("") != default
    || lockKey.Value != -6502278405708100228L
    || lockKey != new PostgreSqlLockKey(-6502278405708100228L))
{
    throw new InvalidOperationException("Validated key package checks failed.");
}

Console.WriteLine(bookingMessage);
Console.WriteLine(lookupMessage);
Console.WriteLine(passengerMessage);
Console.WriteLine(asyncLookupMessage);
Console.WriteLine(asyncPassengerMessage);
Console.WriteLine(invalidBookingMessage);

return;

static Result<string> CreateBooking(string tourCode, string passengerName)
{
    var validationErrors = new ValidationErrors();

    if (string.IsNullOrWhiteSpace(tourCode))
    {
        validationErrors.Add(Result.Invalid("Booking request is invalid.", "tourCode", "Tour code is required."));
    }

    if (string.IsNullOrWhiteSpace(passengerName))
    {
        validationErrors.Add(Result.Invalid("Booking request is invalid.", "passengerName", "Passenger name is required."));
    }

    if (validationErrors.HasErrors)
    {
        return validationErrors.ToResult<string>();
    }

    return Result.Created($"{tourCode}-{passengerName[..1].ToUpperInvariant()}001");
}

static Option<string> FindTourSummary(string tourCode) =>
    tourCode == "VT-42"
        ? Option.Some("VT-42 | Porto river ride")
        : Option.None<string>();

static Task<Option<string>> FindTourSummaryAsync(string tourCode) =>
    Task.FromResult(FindTourSummary(tourCode));

static string? GetPassengerNickname(string passengerName) =>
    passengerName == "Ada Lovelace"
        ? "Ada"
        : null;

static string FormatValidationFailure(ResultError error)
{
    if (error.ValidationErrors is null)
    {
        return $"Booking failed: {error.Detail}";
    }

    var messages = error.ValidationErrors
        .SelectMany(static pair => pair.Value.Select(message => $"{pair.Key}={message}"));

    return $"Booking failed: {error.Detail} [{string.Join(", ", messages)}]";
}
