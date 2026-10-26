/**
 * Creates a handleSummary function that writes the end-of-test summary to /results/<name>-summary.json.
 * The summary holds options, metrics, and checks only: it drops setup_data, which carries the access token that
 * setup() returned, and it never holds environment values.
 */
export function createSummaryHandler(name) {
  return function handleSummary(data) {
    const summary = Object.assign({}, data);
    delete summary.setup_data;

    return { [`/results/${name}-summary.json`]: JSON.stringify(summary, null, 2) };
  };
}
