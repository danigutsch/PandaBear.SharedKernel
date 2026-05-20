using SharedKernel.Results;

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

Console.WriteLine(bookingMessage);
Console.WriteLine(lookupMessage);
Console.WriteLine(passengerMessage);

return;

static Result<string> CreateBooking(string tourCode, string passengerName)
{
    if (string.IsNullOrWhiteSpace(tourCode))
    {
        return Result.Invalid<string>("Booking request is invalid.", "tourCode", "Tour code is required.");
    }

    if (string.IsNullOrWhiteSpace(passengerName))
    {
        return Result.Invalid<string>("Booking request is invalid.", "passengerName", "Passenger name is required.");
    }

    return Result.Ok($"{tourCode}-{passengerName[..1].ToUpperInvariant()}001");
}

static Result<string> FindTourSummary(string tourCode) =>
    tourCode == "VT-42"
        ? Result.Ok("VT-42 | Porto river ride")
        : Result.NotFound<string>($"Tour '{tourCode}' was not found.");

static string? GetPassengerNickname(string passengerName) =>
    passengerName == "Ada Lovelace"
        ? "Ada"
        : null;
