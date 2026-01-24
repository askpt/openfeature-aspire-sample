import { Suspense } from "react";
import Home from "./components/Home";
import ChatBot from "./components/ChatBot";
import "./App.css";
import { OpenFeatureProvider } from "@openfeature/react-sdk";

function App() {
  return (
    <OpenFeatureProvider>
      <Suspense fallback={<div className="loading">Initializing...</div>}>
        <div className="App">
          <Home />
          <ChatBot />
        </div>
      </Suspense>
    </OpenFeatureProvider>
  );
}

export default App;
