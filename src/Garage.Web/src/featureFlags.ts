import { OFREPWebProvider } from "@openfeature/ofrep-web-provider";
import { EvaluationContext, OpenFeature } from "@openfeature/react-sdk";

export function initializeFeatureFlags(ofrepUrl: string) {
  if (!ofrepUrl) {
    console.warn(
      "OFREP URL not configured. Feature flags will not be initialized."
    );
    return;
  }

  // Get user id from local storage
  const userId = localStorage.getItem("userId") || "1";

  const context: EvaluationContext = {
    targetingKey: userId,
    userId,
  };

  console.log("Initializing OpenFeature");
  console.log("OFREP URL:", ofrepUrl);

  // Set context and provider (React SDK handles initialization automatically)
  OpenFeature.setContext(context);
  OpenFeature.setProvider(
    new OFREPWebProvider({
      baseUrl: ofrepUrl,
      pollInterval: 10000,
    })
  );

  console.log("OpenFeature initialized successfully");
}
