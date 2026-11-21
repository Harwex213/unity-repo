import { createRoot } from "react-dom/client";
import { createRegistry } from "./domain/registry-creator";
import { createStore, StoreProvider } from "./store/store";
import { App } from "./ui/app";

const main = () => {
  const container = document.querySelector("#root");
  if (!container) {
    throw new Error("No root was found to mount app");
  }

  const store = createStore();
  const registry = createRegistry(store);
  registry.startRouteSyncAction();
  void registry.loadBoardAction("buildings");
  void registry.loadBoardAction("technologies");

  createRoot(container).render(
    <StoreProvider value={store}>
      <App registry={registry} />
    </StoreProvider>
  );
};

main();
