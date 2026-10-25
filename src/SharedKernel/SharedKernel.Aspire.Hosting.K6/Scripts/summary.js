/**
 * Creates a handleSummary function that writes the end-of-test summary to /results/<name>-summary.json.
 * The summary holds options, metrics, and checks only; it never holds environment values or tokens.
 */
export function createSummaryHandler(name) {
  return function handleSummary(data) {
    return { [`/results/${name}-summary.json`]: JSON.stringify(data, null, 2) };
  };
}
