import { SimpleSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { DocumentLoadInstrumentation } from "@opentelemetry/instrumentation-document-load";
import { FetchInstrumentation } from "@opentelemetry/instrumentation-fetch";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-proto";
import { OTLPLogExporter } from "@opentelemetry/exporter-logs-otlp-proto";
import { OTLPMetricExporter } from "@opentelemetry/exporter-metrics-otlp-proto";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";
import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import { ZoneContextManager } from "@opentelemetry/context-zone";
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from "@opentelemetry/sdk-logs";
import { logs } from "@opentelemetry/api-logs";
import {
  MeterProvider,
  PeriodicExportingMetricReader,
} from "@opentelemetry/sdk-metrics";
import { metrics } from "@opentelemetry/api";

/**
 * Metrics API for recording application metrics
 */
export interface MetricsAPI {
  recordPageView: () => void;
  recordUserIdChange: () => void;
}

// Module-level metrics API that will be initialized by setupMetrics
let metricsAPI: MetricsAPI | null = null;

/**
 * Get the metrics API for recording application metrics
 * @returns The metrics API or null if telemetry is not initialized
 */
export function getMetricsAPI(): MetricsAPI | null {
  return metricsAPI;
}

/**
 * Initialize OpenTelemetry for the web application per Aspire documentation
 * https://aspire.dev/dashboard/enable-browser-telemetry/
 *
 * @param otlpUrl - The OTLP HTTP endpoint URL from OTEL_EXPORTER_OTLP_ENDPOINT
 * @param headers - The OTLP headers from OTEL_EXPORTER_OTLP_HEADERS (comma-separated key=value pairs)
 * @param resourceAttributes - Resource attributes from OTEL_RESOURCE_ATTRIBUTES (comma-separated key=value pairs)
 */
export function initializeTelemetry(
  otlpUrl: string,
  headers: string,
  resourceAttributes: string
) {
  if (!otlpUrl) {
    console.warn("OTEL endpoint not configured. Telemetry will not be sent.");
    return;
  }
  console.log("Initializing OpenTelemetry for browser");
  const attributes = parseDelimitedValues(resourceAttributes);
  attributes[ATTR_SERVICE_NAME] = attributes[ATTR_SERVICE_NAME] || "web";

  // ===== TRACES SETUP =====
  setupTraces(otlpUrl, headers, attributes);

  // ===== METRICS SETUP =====
  setupMetrics(otlpUrl, headers, attributes);

  // ===== LOGS SETUP =====
  setupLogs(otlpUrl, headers, attributes);

  console.log("OpenTelemetry initialized successfully");
}

/**
 * Parse comma-separated key=value pairs into an object
 * Example: "key1=value1,key2=value2" => { key1: "value1", key2: "value2" }
 */
function parseDelimitedValues(s: string): Record<string, string> {
  if (!s) return {};

  const pairs = s.split(","); // Split by comma
  const result: Record<string, string> = {};

  pairs.forEach((pair) => {
    const [key, value] = pair.split("="); // Split by equal sign
    if (key && value) {
      result[key.trim()] = value.trim(); // Add to the object, trimming spaces
    }
  });

  return result;
}

function setupTraces(
  otlpUrl: string,
  headers: string,
  attributes: Record<string, string>
) {
  const otlpOptions = {
    url: `${otlpUrl}/v1/traces`,
    headers: parseDelimitedValues(headers),
  };

  const provider = new WebTracerProvider({
    resource: resourceFromAttributes(attributes),
    spanProcessors: [
      new SimpleSpanProcessor(new OTLPTraceExporter(otlpOptions)),
    ],
  });

  provider.register({
    // Prefer ZoneContextManager: supports asynchronous operations
    contextManager: new ZoneContextManager(),
  });

  // Register instrumentations for automatic tracing
  registerInstrumentations({
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        // Propagate trace context to all outgoing requests except OTLP endpoints
        propagateTraceHeaderCorsUrls: [/.+/],
        clearTimingResources: true,
        // Ignore OTLP telemetry requests to prevent infinite loop
        ignoreUrls: [/\/v1\/(traces|logs|metrics)/],
      }),
    ],
  });
}
function setupMetrics(
  otlpUrl: string,
  headers: string,
  attributes: Record<string, string>
) {
  const metricExporter = new OTLPMetricExporter({
    url: `${otlpUrl}/v1/metrics`,
    headers: parseDelimitedValues(headers),
  });

  const metricReader = new PeriodicExportingMetricReader({
    exporter: metricExporter,
    exportIntervalMillis: 10000, // Export metrics every 10 seconds
  });

  const meterProvider = new MeterProvider({
    resource: resourceFromAttributes(attributes),
    readers: [metricReader],
  });

  metrics.setGlobalMeterProvider(meterProvider);

  // Create meters and counters for app-specific metrics
  const meter = metrics.getMeter("web-app-metrics");
  const pageViewCounter = meter.createCounter("page.views", {
    description: "Count of page views",
    unit: "1",
  });
  const userIdChangeCounter = meter.createCounter("user.id.changes", {
    description: "Count of user ID changes",
    unit: "1",
  });

  // Record initial page view
  pageViewCounter.add(1);

  // Initialize the module-level metrics API
  metricsAPI = {
    recordPageView: () => {
      pageViewCounter.add(1);
    },
    recordUserIdChange: () => {
      userIdChangeCounter.add(1);
    },
  };
}
function setupLogs(
  otlpUrl: string,
  headers: string,
  attributes: Record<string, string>
) {
  const logExporter = new OTLPLogExporter({
    url: `${otlpUrl}/v1/logs`,
    headers: parseDelimitedValues(headers),
  });

  const loggerProvider = new LoggerProvider({
    resource: resourceFromAttributes(attributes),
    processors: [new BatchLogRecordProcessor(logExporter)],
  });

  logs.setGlobalLoggerProvider(loggerProvider);

  // Forward console logs to OTLP
  const logger = logs.getLogger("console-logger");

  const originalConsole = {
    log: console.log,
    warn: console.warn,
    error: console.error,
    info: console.info,
    debug: console.debug,
  };

  // Helper to emit log records
  const emitLog = (level: string, args: any[]) => {
    const message = args
      .map((arg) =>
        typeof arg === "object" ? JSON.stringify(arg) : String(arg)
      )
      .join(" ");

    logger.emit({
      severityText: level,
      body: message,
      timestamp: Date.now(),
    });
  };

  // Override console methods
  console.log = (...args: any[]) => {
    originalConsole.log(...args);
    emitLog("INFO", args);
  };

  console.info = (...args: any[]) => {
    originalConsole.info(...args);
    emitLog("INFO", args);
  };

  console.warn = (...args: any[]) => {
    originalConsole.warn(...args);
    emitLog("WARN", args);
  };

  console.error = (...args: any[]) => {
    originalConsole.error(...args);
    emitLog("ERROR", args);
  };

  console.debug = (...args: any[]) => {
    originalConsole.debug(...args);
    emitLog("DEBUG", args);
  };

  // Track unhandled errors and promise rejections
  window.addEventListener("error", (event) => {
    logger.emit({
      severityText: "ERROR",
      body: `Uncaught error: ${event.message} at ${event.filename}:${event.lineno}:${event.colno}`,
      timestamp: Date.now(),
      attributes: {
        "error.type": "uncaught_error",
        "error.message": event.message,
        "error.filename": event.filename,
        "error.lineno": event.lineno,
        "error.colno": event.colno,
      },
    });
  });

  window.addEventListener("unhandledrejection", (event) => {
    logger.emit({
      severityText: "ERROR",
      body: `Unhandled promise rejection: ${event.reason}`,
      timestamp: Date.now(),
      attributes: {
        "error.type": "unhandled_promise_rejection",
        "error.reason": String(event.reason),
      },
    });
  });
}
