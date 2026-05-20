using Mediator.Sample;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Mediator;

var services = new ServiceCollection();
services.AddSharedKernelMediator();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

var summary = await mediator.Send(new LookupTourSummary("VT-42"), CancellationToken.None);
var bookingCode = await mediator.Send(
    new CreateBooking("VT-42", "Ada Lovelace"),
    CancellationToken.None);

await mediator.Publish(new TourBooked(bookingCode, "Ada Lovelace"), CancellationToken.None);

var streamedCodes = new List<string>();
await foreach (var code in mediator.Send(new StreamTourCodes(3), CancellationToken.None))
{
    streamedCodes.Add(code);
}

Console.WriteLine(summary);
Console.WriteLine($"Created booking: {bookingCode}");
Console.WriteLine($"Streamed tour codes: {string.Join(", ", streamedCodes)}");
