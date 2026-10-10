import { createRoot } from "react-dom/client";
import { loadCatalog } from "./data/load-data";
import { App } from "./ui/app";

const main = () => {
  const container = document.querySelector("#root");
  if (!container) {
    throw new Error("No root was found to mount app");
  }

  // Validation runs once here. The data is static after the build.
  const catalog = loadCatalog();

  createRoot(container).render(<App catalog={catalog} />);
};

main();
