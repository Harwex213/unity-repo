import { useSignals } from "@preact/signals-react/runtime";
import { ICONS } from "../../core/icons";
import { useStore } from "../../store/store";
import { Icon } from "./icon";

/** The current step of the stronghold's soil cleansing, over the canvas. */
const SoilCleanseHint = () => {
  useSignals();
  const store = useStore();
  const mode = store.ui.soilCleanse.value;

  if (!mode) {
    return null;
  }

  const isTargetStep = mode.sacrificeHexId !== null;

  return (
    <div className={`soil-hint ${isTargetStep ? "soil-hint--target" : ""}`} role="status">
      <Icon src={ICONS.toxicity} label="" />

      <span className="soil-hint__step">
        {isTargetStep ? "Шаг 2 из 2" : "Шаг 1 из 2"}
      </span>

      <span className="soil-hint__text">
        {isTargetStep ? "Выберите гекс для очистки" : "Выберите гекс для уничтожения"}
      </span>

      <span className="soil-hint__cancel">
        {"Esc или ПКМ — отмена"}
      </span>
    </div>
  );
};

export { SoilCleanseHint };
