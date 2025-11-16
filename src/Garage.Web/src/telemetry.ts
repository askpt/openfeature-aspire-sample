import { SimpleSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { DocumentLoadInstrumentation } from "@opentelemetry/instrumentation-document-load";
import { FetchInstrumentation } from "@opentelemetry/instrumentation-fetch";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-proto";
import { OTLPLogExporter } from "@opentelemetry/exporter-logs-otlp-proto";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { Resource } from "@opentelemetry/resources";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";
import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import { ZoneContextManager } from "@opentelemetry/context-zone";
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from "@opentelemetry/sdk-logs";
import { logs } from "@opentelemetry/api-logs";

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
  console.log("OTLP Endpoint:", otlpUrl);

  const otlpOptions = {
    url: `${otlpUrl}/v1/traces`,
    headers: parseDelimitedValues(headers),
  };

  const attributes = parseDelimitedValues(resourceAttributes);
  attributes[ATTR_SERVICE_NAME] = attributes[ATTR_SERVICE_NAME] || "web";

  const provider = new WebTracerProvider({
    resource: new Resource(attributes),
  });

  // Add OTLP exporter to send telemetry to Aspire Dashboard
  provider.addSpanProcessor(
    new SimpleSpanProcessor(new OTLPTraceExporter(otlpOptions))
  );

  provider.register({
    // Prefer ZoneContextManager: supports asynchronous operations
    contextManager: new ZoneContextManager(),
  });

  // ===== LOGGING SETUP =====
  const logExporter = new OTLPLogExporter({
    url: `${otlpUrl}/v1/logs`,
    headers: parseDelimitedValues(headers),
  });

  const loggerProvider = new LoggerProvider({
    resource: new Resource(attributes),
  });

  loggerProvider.addLogRecordProcessor(
    new BatchLogRecordProcessor(logExporter)
  );

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

  // Register instrumentations for automatic tracing
  registerInstrumentations({
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        // Propagate trace context to all outgoing requests
        propagateTraceHeaderCorsUrls: [/.+/],
        clearTimingResources: true,
      }),
    ],
  });

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
