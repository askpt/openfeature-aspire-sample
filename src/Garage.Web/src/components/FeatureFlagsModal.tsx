import { useState, useEffect } from "react";
import "./FeatureFlagsModal.css";

interface FeatureFlagsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface FlagState {
  enabled: boolean;
  status: "idle" | "success" | "error";
}

const FeatureFlagsModal = ({ isOpen, onClose }: FeatureFlagsModalProps) => {
  const [enableDemo, setEnableDemo] = useState<FlagState>({
    enabled: false,
    status: "idle",
  });
  const [loading, setLoading] = useState(true);

  const userId = localStorage.getItem("userId") || "1";

  useEffect(() => {
    if (isOpen) {
      fetchFlags();
    }
  }, [isOpen]);

  const fetchFlags = async () => {
    try {
      setLoading(true);
      const response = await fetch(`/flags/?userId=${userId}`);
      if (response.ok) {
        const data = await response.json();
        setEnableDemo({
          enabled: data.flags["enable-demo"] || false,
          status: "idle",
        });
      }
    } catch (err) {
      console.error("Failed to fetch flags:", err);
    } finally {
      setLoading(false);
    }
  };

  const handleToggle = async (flagKey: string, newValue: boolean) => {
    // Reset status before making request
    setEnableDemo((prev) => ({ ...prev, status: "idle" }));

    try {
      const response = await fetch("/flags/", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          userId,
          flagKey,
          enabled: newValue,
        }),
      });

      if (response.ok) {
        setEnableDemo({ enabled: newValue, status: "success" });
        // Hide success indicator after 2 seconds
        setTimeout(() => {
          setEnableDemo((prev) => ({ ...prev, status: "idle" }));
        }, 2000);
      } else {
        setEnableDemo((prev) => ({ ...prev, status: "error" }));
        // Hide error indicator after 2 seconds
        setTimeout(() => {
          setEnableDemo((prev) => ({ ...prev, status: "idle" }));
        }, 2000);
      }
    } catch (err) {
      console.error("Failed to update flag:", err);
      setEnableDemo((prev) => ({ ...prev, status: "error" }));
      // Hide error indicator after 2 seconds
      setTimeout(() => {
        setEnableDemo((prev) => ({ ...prev, status: "idle" }));
      }, 2000);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Feature Flags</h2>
          <button className="modal-close-btn" onClick={onClose}>
            ×
          </button>
        </div>
        <div className="modal-body">
          {loading ? (
            <div className="loading">Loading flags...</div>
          ) : (
            <div className="flag-list">
              <div className="flag-item">
                <div className="flag-info">
                  <span className="flag-name">enable-demo</span>
                  <span className="flag-description">
                    Enable demo features for this user
                  </span>
                </div>
                <div className="flag-toggle-container">
                  <label className="toggle-switch">
                    <input
                      type="checkbox"
                      checked={enableDemo.enabled}
                      onChange={(e) =>
                        handleToggle("enable-demo", e.target.checked)
                      }
                    />
                    <span className="toggle-slider"></span>
                  </label>
                  {enableDemo.status === "success" && (
                    <span className="status-icon success">✓</span>
                  )}
                  {enableDemo.status === "error" && (
                    <span className="status-icon error">✗</span>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default FeatureFlagsModal;
