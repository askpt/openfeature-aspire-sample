import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { OpenFeature, EvaluationContext } from "@openfeature/react-sdk";
import { OFREPWebProvider } from "@openfeature/ofrep-web-provider";

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
