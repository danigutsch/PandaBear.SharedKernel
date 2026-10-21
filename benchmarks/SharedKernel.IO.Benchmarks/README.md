# IO real HTTP comparison

Run from the repository root:

```sh
dotnet run --project benchmarks/SharedKernel.IO.Benchmarks -c Release
```

This dependency-free executable starts Kestrel on a randomized loopback port and sends actual HTTP
request bodies with HttpClient. It deliberately uses a focused host instead of Aspire: no external
services are needed to compare spooling. It never uses TestServer. Each strategy receives three warmups
and twenty measured sequential requests at 64 KiB and 8 MiB. Each request materializes its body and runs
three SHA-256 passes as a deterministic scanning/decoding/storage-read surrogate. The maximum is 32 MiB;
the lease threshold is 1 MiB. The process exits and disposes the host and all temporary files.

The strategies are the current bounded MemoryStream copy shape, direct temporary-file spooling, and
the lease. No BenchmarkDotNet, k6, or other package is added. Redirect output to an ignored local artifact
when retaining new runs. Do not commit generated payloads or raw traces containing user data.

## Evidence before implementation

Linux x64, .NET SDK 10.0.402, 2026-10-21. The first run preceded lease implementation and compared the
existing bounded memory copy against direct file spooling:

| Strategy | Bytes | Mean ms/request | Requests/second | Allocated bytes/request |
| --- | ---: | ---: | ---: | ---: |
| Memory | 65,536 | 1.391 | 718.685 | 280,989 |
| File | 65,536 | 1.326 | 754.080 | 91,019 |
| Memory | 8,388,608 | 97.170 | 10.291 | 27,094,023 |
| File | 8,388,608 | 98.745 | 10.127 | 119,718 |

This justified a threshold-spilling primitive to bound retained managed content for larger multi-pass
inputs. It did not establish a universal threshold or a latency improvement.

## Lease comparison

Same environment and command after implementation:

| Strategy | Bytes | Mean ms/request | Requests/second | Allocated bytes/request |
| --- | ---: | ---: | ---: | ---: |
| Memory | 65,536 | 6.513 | 153.546 | 217,055 |
| File | 65,536 | 4.094 | 244.265 | 90,166 |
| Lease | 65,536 | 3.359 | 297.727 | 203,494 |
| Memory | 8,388,608 | 145.343 | 6.880 | 24,371,909 |
| File | 8,388,608 | 179.251 | 5.579 | 103,742 |
| Lease | 8,388,608 | 171.053 | 5.846 | 2,701,141 |

Allocation counts cover the whole server/client process, exclude the preallocated request payload,
and include stream growth, networking, hashing, and fixed copy buffers. They are total allocation,
not peak live memory. File cache and developer-machine scheduling make latency noisy. The lease retains
small content in memory and uses a fixed copy buffer while bounding larger-input storage.
The observed larger-input allocation reduction supports the package's memory-bound behavior; correctness
tests enforce size and threshold boundaries independently of timing.

This is raw request-body evidence for the library decision, not an application end-to-end performance
claim: no multipart parsing, antivirus provider, image decoder, persistence, concurrency, or slow client.
ViajantesTurismo#700 retains the full upload/scan benchmark, and #705 retains measured application
adoption. Those application tasks must preserve auth, validation, cancellation, scanning and compensation.
A local Linux packed JIT consumer also verified multi-pass reads, owner-only Unix file permissions,
and no leaked files after success, oversize rejection, read failure, and cancellation in an isolated
temporary directory. Windows and packed NativeAOT execution remain unverified for this new package.
