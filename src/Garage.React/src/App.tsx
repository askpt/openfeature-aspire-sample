import { useState, useEffect } from "react";
import Home from "./components/Home";
import "./App.css";
import { OFREPWebProvider } from "@openfeature/ofrep-web-provider";
import { EvaluationContext, OpenFeature } from "@openfeature/web-sdk";

function App() {
  const [isFeatureFlagsReady, setIsFeatureFlagsReady] = useState(false);

  useEffect(() => {
    const initializeFeatureFlags = async () => {
      try {
        const ofrepServiceUrl = import.meta.env.VITE_OFREP_SERVICE_URL || "";

        // Get user id from local storage
        const userId = localStorage.getItem("userId") || "1";

        const context: EvaluationContext = {
          targetingKey: userId,
          userId,
        };
        OpenFeature.setContext(context);

        await OpenFeature.setProviderAndWait(
          new OFREPWebProvider({
            baseUrl: ofrepServiceUrl,
            pollInterval: 10000,
          })
        );

        console.log("OFREP OpenFeature provider initialized");
      } catch (error) {
        console.warn("Failed to initialize OpenFeature provider:", error);
      } finally {
        setIsFeatureFlagsReady(true);
      }
    };

    initializeFeatureFlags();
  }, []);

  if (!isFeatureFlagsReady) {
    return <div className="loading">Initializing...</div>;
  }

  return (
    <div className="App">
      <Home />
    </div>
  );
}

export default App;
