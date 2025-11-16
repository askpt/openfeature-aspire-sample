import { getMetricsAPI } from "./telemetry";

/**
 * Record a page view metric
 */
export function recordPageView() {
  const metrics = getMetricsAPI();
  if (metrics) {
    metrics.recordPageView();
  }
}

/**
 * Record a user ID change metric
 */
export function recordUserIdChange() {
  const metrics = getMetricsAPI();
  if (metrics) {
    metrics.recordUserIdChange();
  }
}
