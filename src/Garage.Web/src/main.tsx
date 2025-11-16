import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { OpenFeature, EvaluationContext } from "@openfeature/react-sdk";
import { OFREPWebProvider } from "@openfeature/ofrep-web-provider";
import { initializeTelemetry } from "./telemetry";

// Initialize OpenTelemetry FIRST (if Aspire provides the endpoint)
const otelEndpoint = import.meta.env.VITE_OTEL_ENDPOINT;
const otelHeaders = import.meta.env.VITE_OTEL_HEADERS || "";
const otelResourceAttributes =
  import.meta.env.VITE_OTEL_RESOURCE_ATTRIBUTES || "";

if (otelEndpoint) {
  initializeTelemetry(otelEndpoint, otelHeaders, otelResourceAttributes);
}

// Initialize OpenFeature provider before rendering
const ofrepServiceUrl = import.meta.env.VITE_OFREP_SERVICE_URL || "";

// Get user id from local storage
const userId = localStorage.getItem("userId") || "1";

const context: EvaluationContext = {
  targetingKey: userId,
  userId,
};

// Set context and provider (React SDK handles initialization automatically)
OpenFeature.setContext(context);
OpenFeature.setProvider(
  new OFREPWebProvider({
    baseUrl: ofrepServiceUrl,
    pollInterval: 10000,
  })
);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
