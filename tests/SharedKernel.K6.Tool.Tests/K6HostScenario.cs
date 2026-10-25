using System.Globalization;

namespace PandaBear.K6.Tool.Tests;

/// <summary>The configuration and fake host script of a scenario with a managed host.</summary>
internal static class K6HostScenario
{
    public static string Configuration(string engine = "podman", int readyTimeoutSeconds = 10, int healthTimeoutSeconds = 10) => $$"""
        {
          "version": 1,
          "scriptDirectory": "perf/k6",
          "resultsDirectory": "perf/results",
          "container": { "engine": "{{engine}}", "image": "{{K6TestRepository.Image}}" },
          "scenarios": [
            {
              "name": "upload",
              "script": "scenarios/smoke.js",
              "baseUrlVariable": "APP_UPLOAD_URL",
              "environment": ["APP_PAYLOAD_BYTES"],
              "host": {
                "command": "app-host",
                "arguments": ["run", "--urls", "{listenUrl}"],
                "readyFileVariable": "APP_READY_FILE",
                "healthPath": "/health",
                "readyTimeoutSeconds": {{readyTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}},
                "healthTimeoutSeconds": {{healthTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}}
              }
            }
          ]
        }
        """;

    public static string Port(HealthServer server) => server.Port.ToString(CultureInfo.InvariantCulture);

    public static string Ready(string url) => "printf '%s\\n' " + FakeExecutable.Quote(url) + " > \"$APP_READY_FILE\"\n";
}
