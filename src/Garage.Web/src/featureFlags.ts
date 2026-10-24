import { OFREPWebProvider } from "@openfeature/ofrep-web-provider";
import { EvaluationContext, OpenFeature } from "@openfeature/react-sdk";

export function initializeFeatureFlags() {
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
      // Must be absolute: the provider resolves flagd's advertised SSE stream
      // (/ofrep/v1/sse) against this origin and `new URL()` rejects relative
      // bases. Both /ofrep/v1/evaluate/flags and /ofrep/v1/sse are proxied
      // same-origin to flagd (Vite in dev, nginx in the container image).
      baseUrl: window.location.origin,
      // Fallback only; polling is suspended while the SSE stream is connected.
      pollInterval: 10000,
    })
  );
}
