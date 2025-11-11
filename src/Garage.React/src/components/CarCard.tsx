import { Winner } from "../types/Winner";
import "./CarCard.css";

interface CarCardProps {
  car: Winner;
  onOwnershipChanged: (car: Winner) => void;
}

const CarCard: React.FC<CarCardProps> = ({ car, onOwnershipChanged }) => {
  const toggleOwnership = (event: React.ChangeEvent<HTMLInputElement>) => {
    const updatedCar = { ...car, isOwned: event.target.checked };
    onOwnershipChanged(updatedCar);
    console.log(`Toggling ownership for car ${car.year}`);
  };

  return (
    <div className="car-card">
      <div className="car-header">
        <span className="car-year">{car.year}</span>
        <div className="car-actions">
          <input
            type="checkbox"
            className="ownership-checkbox"
            checked={car.isOwned}
            onChange={toggleOwnership}
          />
        </div>
      </div>
      <h3>{car.model}</h3>
      <span className="car-class">{car.class}</span>
      {car.image ? (
        <img
          src={car.image}
          alt={`${car.year} ${car.manufacturer} ${car.model}`}
          className="car-image"
        />
      ) : (
        <div className="car-image-placeholder">
          <span>No Image Available</span>
        </div>
      )}
      <div className="car-details">
        <div className="detail-row">
          <span className="detail-icon">🔧</span>
          <span>Engine: {car.engine}</span>
        </div>
        <div className="detail-row">
          <span className="detail-icon">🏁</span>
          <span>Drivers:</span>
        </div>
        <div className="drivers">{car.drivers.join(", ")}</div>
      </div>
    </div>
  );
};

export default CarCard;
