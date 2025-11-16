/**
 * Helper functions to record metrics from React components
 */

interface OtelMetrics {
  recordPageView: () => void;
  recordUserIdChange: () => void;
}

/**
 * Record a page view metric
 */
export function recordPageView() {
  const metrics = (window as any).__OTEL_METRICS__ as OtelMetrics | undefined;
  if (metrics) {
    metrics.recordPageView();
  }
}

/**
 * Record a user ID change metric
 */
export function recordUserIdChange() {
  const metrics = (window as any).__OTEL_METRICS__ as OtelMetrics | undefined;
  if (metrics) {
    metrics.recordUserIdChange();
  }
}
