import "@fontsource/cormorant-sc/cyrillic-500.css";
import "@fontsource/forum/cyrillic-400.css";
import "./ui/hud.css";
import { createRoot } from "react-dom/client";
import { App } from "./ui/app";

const root = document.getElementById("root");
if (!root) {
  throw new Error("The page has no #root element");
}

createRoot(root).render(<App />);
