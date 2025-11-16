import {
  ConsoleSpanExporter,
  SimpleSpanProcessor,
} from "@opentelemetry/sdk-trace-base";
import { DocumentLoadInstrumentation } from "@opentelemetry/instrumentation-document-load";
import { FetchInstrumentation } from "@opentelemetry/instrumentation-fetch";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-proto";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { Resource } from "@opentelemetry/resources";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";
import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import { ZoneContextManager } from "@opentelemetry/context-zone";

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

  // Add console exporter for debugging (optional - remove in production)
  provider.addSpanProcessor(new SimpleSpanProcessor(new ConsoleSpanExporter()));

  // Add OTLP exporter to send telemetry to Aspire Dashboard
  provider.addSpanProcessor(
    new SimpleSpanProcessor(new OTLPTraceExporter(otlpOptions))
  );

  provider.register({
    // Prefer ZoneContextManager: supports asynchronous operations
    contextManager: new ZoneContextManager(),
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
