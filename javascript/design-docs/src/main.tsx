import { createRoot } from "react-dom/client";
import { DOCUMENTS } from "./documents";
import { App } from "./ui/app";

const main = () => {
  const container = document.querySelector("#root");
  if (!container) {
    throw new Error("No root was found to mount app");
  }

  createRoot(container).render(<App documents={DOCUMENTS} />);
};

main();
