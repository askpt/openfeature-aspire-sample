import { useState, useEffect, useCallback } from "react";
import "./FeatureFlagsModal.css";

interface FeatureFlagsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface FlagState {
  enabled: boolean;
  status: "idle" | "success" | "error";
}

type FlagsMap = Record<string, FlagState>;

const FeatureFlagsModal = ({ isOpen, onClose }: FeatureFlagsModalProps) => {
  const [flags, setFlags] = useState<FlagsMap>({});
  const [loading, setLoading] = useState(true);

  const userId = localStorage.getItem("userId") || "1";

  const fetchFlags = useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetch(`/flags/?userId=${userId}`);
      if (response.ok) {
        const data = await response.json();
        // data is { "enable-demo": true, "enable-demo2": false, ... }
        const flagsData: FlagsMap = {};
        for (const [key, value] of Object.entries(data)) {
          flagsData[key] = {
            enabled: value as boolean,
            status: "idle",
          };
        }
        setFlags(flagsData);
      }
    } catch (err) {
      console.error("Failed to fetch flags:", err);
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    if (isOpen) {
      fetchFlags();
    }
  }, [isOpen, fetchFlags]);

  // Clean up status indicators after 2 seconds
  useEffect(() => {
    const timeoutIds: NodeJS.Timeout[] = [];

    Object.entries(flags).forEach(([flagKey, flagState]) => {
      if (flagState.status !== "idle") {
        const timeoutId = setTimeout(() => {
          setFlags((prev) => ({
            ...prev,
            [flagKey]: { ...prev[flagKey], status: "idle" },
          }));
        }, 2000);
        timeoutIds.push(timeoutId);
      }
    });

    return () => {
      timeoutIds.forEach((id) => clearTimeout(id));
    };
  }, [flags]);

  const handleToggle = async (flagKey: string, newValue: boolean) => {
    // Reset status before making request
    setFlags((prev) => ({
      ...prev,
      [flagKey]: { ...prev[flagKey], status: "idle" },
    }));

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
        setFlags((prev) => ({
          ...prev,
          [flagKey]: { enabled: newValue, status: "success" },
        }));
      } else {
        setFlags((prev) => ({
          ...prev,
          [flagKey]: { ...prev[flagKey], status: "error" },
        }));
      }
    } catch (err) {
      console.error("Failed to update flag:", err);
      setFlags((prev) => ({
        ...prev,
        [flagKey]: { ...prev[flagKey], status: "error" },
      }));
    }
  };

  if (!isOpen) return null;

  const flagKeys = Object.keys(flags).sort();

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
          ) : flagKeys.length === 0 ? (
            <div className="no-flags">No flags available</div>
          ) : (
            <div className="flag-list">
              {flagKeys.map((flagKey) => (
                <div key={flagKey} className="flag-item">
                  <div className="flag-info">
                    <span className="flag-name">{flagKey}</span>
                  </div>
                  <div className="flag-toggle-container">
                    <label className="toggle-switch">
                      <input
                        type="checkbox"
                        checked={flags[flagKey].enabled}
                        onChange={(e) => handleToggle(flagKey, e.target.checked)}
                      />
                      <span className="toggle-slider"></span>
                    </label>
                    {flags[flagKey].status === "success" && (
                      <span className="status-icon success">✓</span>
                    )}
                    {flags[flagKey].status === "error" && (
                      <span className="status-icon error">✗</span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default FeatureFlagsModal;
