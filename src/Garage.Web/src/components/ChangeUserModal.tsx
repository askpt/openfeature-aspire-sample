import { useId, useState, type FormEvent, type KeyboardEvent } from "react";
import "./Modal.css";
import "./ChangeUserModal.css";

interface ChangeUserModalProps {
  currentUserId: string;
  onClose: () => void;
  onSubmit: (userId: string) => void;
}

const MAX_USER_ID_LENGTH = 64;

// Mounted only while open, so the form state is reset on every open.
const ChangeUserModal = ({ currentUserId, onClose, onSubmit }: ChangeUserModalProps) => {
  const [userId, setUserId] = useState(currentUserId);
  const inputId = useId();
  const titleId = useId();
  const hintId = useId();

  const trimmedUserId = userId.trim();
  const isUnchanged = trimmedUserId === currentUserId;

  // The submit button is disabled while the ID is blank or unchanged, which also blocks
  // implicit submission with Enter, so only valid, changed IDs reach this handler.
  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (trimmedUserId !== "" && !isUnchanged) {
      onSubmit(trimmedUserId);
    }
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      onClose();
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose} onKeyDown={handleKeyDown}>
      <div
        className="modal-content change-user-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <h2 id={titleId}>Change User</h2>
          <button
            type="button"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Close"
          >
            ×
          </button>
        </div>
        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            <p className="change-user-intro">
              Feature flags are evaluated for this user ID, so targeting rules
              (for example the flags enabled per user in the Feature Flags panel)
              apply to it. Switching user reloads the page to re-evaluate every
              flag.
            </p>
            <label className="change-user-label" htmlFor={inputId}>
              User ID
            </label>
            <input
              id={inputId}
              className="change-user-input"
              type="text"
              value={userId}
              maxLength={MAX_USER_ID_LENGTH}
              autoComplete="off"
              spellCheck={false}
              autoFocus
              aria-describedby={hintId}
              onChange={(e) => setUserId(e.target.value)}
            />
            <p id={hintId} className="change-user-hint">
              Current user: {currentUserId}
            </p>
          </div>
          <div className="modal-footer">
            <button
              type="button"
              className="modal-btn modal-btn-secondary"
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="modal-btn modal-btn-primary"
              disabled={trimmedUserId === "" || isUnchanged}
            >
              Switch User
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ChangeUserModal;
