import { useState, useEffect } from "react";
import { useBooleanFlagValue } from "@openfeature/react-sdk";
import { Winner, FilterType } from "../types/Winner";
import CarCard from "./CarCard";
import FeatureFlagsModal from "./FeatureFlagsModal";
import { recordPageView, recordUserIdChange } from "../metrics";
import "./Home.css";

const Home = () => {
  const [winners, setWinners] = useState<Winner[]>([]);
  const [activeFilter, setActiveFilter] = useState<FilterType>(FilterType.All);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentUserId, setCurrentUserId] = useState<string>(
    localStorage.getItem("userId") || "1"
  );
  const [showFlagsModal, setShowFlagsModal] = useState(false);

  // Use OpenFeature React hooks for feature flags
  const showHeader = useBooleanFlagValue("enable-stats-header", true);
  const showTabs = useBooleanFlagValue("enable-tabs", true);

  const ownedCount = winners.filter((w) => w.isOwned).length;

  const filteredWinners = winners.filter((winner) => {
    switch (activeFilter) {
      case FilterType.Owned:
        return winner.isOwned;
      case FilterType.NotOwned:
        return !winner.isOwned;
      default:
        return true;
    }
  });

  useEffect(() => {
    // Record page view metric
    recordPageView();

    const loadWinners = async () => {
      try {
        setLoading(true);
        // Call API service directly using Aspire service discovery
        const response = await fetch("/api/lemans/winners");
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const winnersData: Winner[] = await response.json();
        setWinners(winnersData);
        console.log(`Loaded ${winnersData.length} winners`);
      } catch (err) {
        console.error("Failed to load winners:", err);
        setError("Failed to load winners");
        setWinners([]);
      } finally {
        setLoading(false);
      }
    };

    loadWinners();
  }, []);

  const handleOwnershipChanged = (updatedCar: Winner) => {
    setWinners((prevWinners) =>
      prevWinners.map((winner) =>
        winner.year === updatedCar.year ? updatedCar : winner
      )
    );
    console.log(`Updated ownership for ${updatedCar.year} ${updatedCar.model}`);
  };

  const setFilter = (filterType: FilterType) => {
    setActiveFilter(filterType);
  };

  const handleChangeUserId = () => {
    const newUserId = prompt("Enter new user ID:", currentUserId);
    if (newUserId && newUserId.trim() !== "") {
      localStorage.setItem("userId", newUserId.trim());
      setCurrentUserId(newUserId.trim());
      // Record user ID change metric
      recordUserIdChange();
      // Reload the page to reinitialize OpenFeature with the new user ID
      window.location.reload();
    }
  };

  if (loading) {
    return <div className="loading">Loading winners...</div>;
  }

  if (error) {
    return <div className="error">{error}</div>;
  }

  const completionPercentage =
    winners.length > 0
      ? Math.round((ownedCount / winners.length) * 100 * 10) / 10
      : 0;

  return (
    <div className="home">
      {showHeader && (
        <div className="garage-header">
          <div className="header-top">
            <div className="title-section">
              <span className="garage-icon">🏆</span>
              <div>
                <h1>Le Mans Collection Tracker</h1>
                <p className="subtitle">
                  Track your model car winners collection of racing legends from
                  the 24h of Le Mans
                </p>
              </div>
            </div>
            <div className="stats-section">
              <div className="stat-item owned">
                <span className="stat-number">{ownedCount}</span>
                <span className="stat-label">Owned</span>
              </div>
              <div className="stat-item owned">
                <span className="stat-number">{winners.length}</span>
                <span className="stat-label">Total</span>
              </div>
              <div className="stat-item complete">
                <span className="stat-number">{completionPercentage}%</span>
                <span className="stat-label">Complete</span>
              </div>
            </div>
          </div>
          <div className="user-section">
            <span className="user-id-label">User ID: {currentUserId}</span>
            <button className="change-user-btn" onClick={handleChangeUserId}>
              Change User
            </button>
            <button
              className="feature-flags-btn"
              onClick={() => setShowFlagsModal(true)}
            >
              Feature Flags
            </button>
          </div>
        </div>
      )}

      <div className="collection-summary">
        <span className="collection-count">
          {filteredWinners.length} of {winners.length} cars
        </span>
      </div>

      {showTabs && (
        <div className="collection-tabs">
          <button
            className={`tab-btn ${
              activeFilter === FilterType.All ? "active" : ""
            }`}
            onClick={() => setFilter(FilterType.All)}
          >
            All Winners ({winners.length})
          </button>
          <button
            className={`tab-btn ${
              activeFilter === FilterType.Owned ? "active" : ""
            }`}
            onClick={() => setFilter(FilterType.Owned)}
          >
            Owned ({ownedCount})
          </button>
          <button
            className={`tab-btn ${
              activeFilter === FilterType.NotOwned ? "active" : ""
            }`}
            onClick={() => setFilter(FilterType.NotOwned)}
          >
            Not Owned ({winners.length - ownedCount})
          </button>
        </div>
      )}

      <div className="car-grid">
        {filteredWinners.map((winner) => (
          <CarCard
            key={winner.year}
            car={winner}
            onOwnershipChanged={handleOwnershipChanged}
          />
        ))}
      </div>

      <FeatureFlagsModal
        isOpen={showFlagsModal}
        onClose={() => setShowFlagsModal(false)}
      />
    </div>
  );
};

export default Home;
