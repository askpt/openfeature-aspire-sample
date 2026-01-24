import { useState, useRef, useEffect } from "react";
import { useBooleanFlagValue } from "@openfeature/react-sdk";
import "./ChatBot.css";

interface Message {
  role: "user" | "assistant" | "error";
  content: string;
  promptStyle?: string;
}

const ChatBot = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([
    {
      role: "assistant",
      content:
        "Hello! I'm your Le Mans expert. Ask me anything about the 24 Hours of Le Mans! 🏎️",
    },
  ]);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [currentPromptStyle, setCurrentPromptStyle] = useState<string>("");
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Feature flag to enable/disable chatbot
  const chatbotEnabled = useBooleanFlagValue("enable-chatbot", true);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const sendMessage = async () => {
    if (!input.trim() || isLoading) return;

    const userMessage = input.trim();
    setInput("");
    setMessages((prev) => [...prev, { role: "user", content: userMessage }]);
    setIsLoading(true);

    try {
      const response = await fetch("/api/chat", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ message: userMessage }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.detail || `HTTP error ${response.status}`);
      }

      const data = await response.json();
      setCurrentPromptStyle(data.prompt_style || "");
      setMessages((prev) => [
        ...prev,
        {
          role: "assistant",
          content: data.response,
          promptStyle: data.prompt_style,
        },
      ]);
    } catch (error) {
      console.error("Chat error:", error);
      setMessages((prev) => [
        ...prev,
        {
          role: "error",
          content:
            error instanceof Error
              ? error.message
              : "Failed to send message. Please try again.",
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  // Don't render if chatbot is disabled
  if (!chatbotEnabled) {
    return null;
  }

  return (
    <div className="chatbot-container">
      {isOpen && (
        <div className="chatbot-window">
          <div className="chatbot-header">
            <span className="chatbot-header-icon">🏁</span>
            <div className="chatbot-header-text">
              <h3>Le Mans Assistant</h3>
              <p>Ask me about Le Mans racing!</p>
            </div>
            {currentPromptStyle && (
              <span className="prompt-style-badge">{currentPromptStyle}</span>
            )}
          </div>

          <div className="chatbot-messages">
            {messages.map((message, index) => (
              <div key={index} className={`chat-message ${message.role}`}>
                {message.content}
              </div>
            ))}
            {isLoading && (
              <div className="chat-message loading">
                <div className="typing-indicator">
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          <div className="chatbot-input-container">
            <input
              type="text"
              className="chatbot-input"
              placeholder="Ask about Le Mans..."
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyPress={handleKeyPress}
              disabled={isLoading}
            />
            <button
              className="chatbot-send-btn"
              onClick={sendMessage}
              disabled={isLoading || !input.trim()}
            >
              ➤
            </button>
          </div>
        </div>
      )}

      <button
        className={`chatbot-bubble ${isOpen ? "open" : ""}`}
        onClick={() => setIsOpen(!isOpen)}
        aria-label={isOpen ? "Close chat" : "Open chat"}
      >
        {isOpen ? "✕" : "💬"}
      </button>
    </div>
  );
};

export default ChatBot;
