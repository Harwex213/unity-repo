import { createRoot } from "react-dom/client";
import { createSession } from "./domain/game-actions";
import { openMainMenuAction } from "./domain/menu-actions";
import { createRegistry } from "./domain/registry-creator";
import { syncRouteFromHash } from "./domain/route-actions";
import { createStore, StoreProvider } from "./store/store";
import { App } from "./ui/app";

declare global {
  interface Window {
    /** Console commands for the prototype. */
    prototype: {
      readonly saveStoreSnapshot: () => Promise<void>;
      readonly loadStoreSnapshot: () => Promise<void>;
    };
  }
}

const main = () => {
  const container = document.querySelector("#root");
  if (!container) {
    throw new Error("No root was found to mount app");
  }

  const root = createRoot(container);

  const store = createStore();

  const registry = createRegistry(store);

  // The session opens on the island, in the stage where the player places the
  // stronghold.
  createSession(store);

  // The main menu opens over the game. The guide waits for the menu to close.
  openMainMenuAction(store);

  // The address bar is the route. One listener reads it back into the store,
  // and the boot sync makes a deep link work on a cold load.
  window.addEventListener("hashchange", () => {
    syncRouteFromHash(store);
  });

  syncRouteFromHash(store);

  window.prototype = {
    saveStoreSnapshot: registry.saveStoreSnapshotAction,
    loadStoreSnapshot: registry.loadStoreSnapshotAction,
  };

  root.render(
    <StoreProvider value={store}>
      <App registry={registry} />
    </StoreProvider>
  );
};

main();
