import { useSignals } from "@preact/signals-react/runtime";
import { BattlePage } from "./pages/battle-page";
import { IslandPage } from "./pages/island-page";
import { WorldPage } from "./pages/world-page";
import { EndGameModal } from "./components/end-game-modal";
import { LearnGuide } from "./components/learn-guide";
import { MainMenu } from "./components/main-menu/main-menu";
import { SlotModal } from "./components/slot-modal";
import { useStore } from "../store/store";
import type { FC } from "react";
import type { TAppRegistry } from "../domain/registry";
import "./app.css";

type TAppProps = {
  registry: TAppRegistry;
};

const App: FC<TAppProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const page = store.route.page.value;
  // While the main menu is open the game is not mounted: its canvases and
  // hotkeys do not run. The game mounts under the menu when its fade-out starts.
  const isMenuOpen = store.ui.mainMenu.value === "open";
  const pageView = page === "world"
    ? <WorldPage registry={registry} />
    : page === "battle"
      ? <BattlePage registry={registry} />
      : <IslandPage registry={registry} />;

  // The toxicity slot opens when the tax phase ends, over whatever page is on
  // screen, and stays until its button takes the player to the world map.
  return (
    <>
      {isMenuOpen ? null : (
        <>
          {pageView}

          <SlotModal registry={registry} />

          {/* The learn guide, on only with the `?guide` flag. */}
          <LearnGuide registry={registry} />

          <EndGameModal registry={registry} />
        </>
      )}

      {/* The main menu, on only with the `?menu` flag. */}
      <MainMenu registry={registry} />
    </>
  );
};

export { App };
