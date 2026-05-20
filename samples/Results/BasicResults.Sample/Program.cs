using SharedKernel.Functional;

var bookingResult = CreateBooking("VT-42", "Ada Lovelace");
var bookingMessage = bookingResult.Match(
    static confirmation => $"Created booking: {confirmation}",
    static error => $"Booking failed: {error.Detail}");

var lookupMessage = FindTourSummary("VT-42").Match(
    static summary => $"Tour summary: {summary}",
    static error => $"Lookup failed: {error.Detail}");

var maybePassenger = Option.FromNullable(GetPassengerNickname("Ada Lovelace"));
var passengerMessage = maybePassenger.Match(
    static nickname => $"Nickname: {nickname}",
    static () => "Nickname: none");

var asyncLookupMessage = await FindTourSummaryAsync("VT-42")
    .Map(static summary => Task.FromResult(summary.ToUpperInvariant()))
    .Match(
        static summary => $"Async tour summary: {summary}",
        static error => $"Async lookup failed: {error.Detail}");

var asyncPassengerOption = await ValueTask.FromResult(Option.FromNullable(GetPassengerNickname("Ada Lovelace")))
    .Map(static nickname => ValueTask.FromResult(nickname.ToUpperInvariant()));
var asyncPassengerMessage = await asyncPassengerOption.Match(
    static nickname => ValueTask.FromResult($"Async nickname: {nickname}"),
    static () => ValueTask.FromResult("Async nickname: none"));

var invalidBookingMessage = CreateBooking("", "").Match(
    static confirmation => $"Created booking: {confirmation}",
    static error => FormatValidationFailure(error));

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

static Result<string> FindTourSummary(string tourCode) =>
    tourCode == "VT-42"
        ? Result.Ok("VT-42 | Porto river ride")
        : Result.NotFound<string>($"Tour '{tourCode}' was not found.");

static Task<Result<string>> FindTourSummaryAsync(string tourCode) =>
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
