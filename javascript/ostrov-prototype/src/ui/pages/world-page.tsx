import { useSignals } from "@preact/signals-react/runtime";
import { useStore } from "../../store/store";
import { EndTurnPanel } from "../components/end-turn-panel";
import { Globe } from "../components/globe";
import { IslandMinimap } from "../components/island-minimap";
import { NoticeToast } from "../components/notice-toast";
import { PlayersPanel } from "../components/players-panel";
import { ResourcesPanel } from "../components/resources-panel";
import { ToxicPanel } from "../components/toxic-panel";
import { TrailEventModal } from "../components/trail-event-modal";
import { TurnPanel } from "../components/turn-panel";
import { WaitingOverlay } from "../components/waiting-overlay";
import { WorldCellPanel } from "../components/world-cell-panel";
import type { FC } from "react";
import type { TAppRegistry } from "../../domain/registry";

type TWorldPageProps = {
  registry: TAppRegistry;
};

/**
 * The exploration phase, on the globe. The wireframe keeps the same shell as
 * the island page minus the build tools: players, turn, resources, end turn.
 */
const WorldPage: FC<TWorldPageProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const waitingClass = store.derived.isHumanReady.value ? "island-page--waiting" : "";

  return (
    <div className={`island-page ${waitingClass}`}>
      <Globe registry={registry} />

      <div className="island-page__top-left">
        <PlayersPanel registry={registry} />
      </div>

      <div className="island-page__top-center">
        <TurnPanel />
      </div>

      {/* The same toxicity meter as on the island page, in the same place. */}
      <div className="island-page__right">
        <ToxicPanel registry={registry} />
      </div>

      <div className="island-page__bottom">
        <div className="world-page__bottom-left">
          <WorldCellPanel registry={registry} />

          <ResourcesPanel registry={registry} />
        </div>

        <div className="island-page__end">
          <IslandMinimap registry={registry} />

          <EndTurnPanel registry={registry} />
        </div>
      </div>

      <WaitingOverlay />

      <NoticeToast />

      <TrailEventModal registry={registry} />
    </div>
  );
};

export { WorldPage };
