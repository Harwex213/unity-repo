// Creates a new prototype folder from a template.
//
// Usage: npm run new -- <id> "<title>" "<description>"
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { ID_PATTERN, PROTOTYPES_DIR, fail, listPrototypes } from "./lib.mjs";

const [id, title, description = ""] = process.argv.slice(2);
if (!id || !ID_PATTERN.test(id) || !title) {
  fail('Usage: npm run new -- <id> "<title>" "<description>". The id is kebab-case.');
}
const dir = join(PROTOTYPES_DIR, id);
if (existsSync(dir)) {
  fail(`The prototype "${id}" already exists.`);
}

const order = Math.max(0, ...listPrototypes().map((item) => item.order)) + 1;

mkdirSync(join(dir, "src"), { recursive: true });
writeFileSync(join(dir, "prototype.json"), `${JSON.stringify({ title, description, order }, null, 2)}\n`);
writeFileSync(
  join(dir, "src", "main.tsx"),
  `import { createRoot } from "react-dom/client";
import { App } from "./app";

const root = document.getElementById("root");
if (!root) {
  throw new Error("The page has no #root element");
}

createRoot(root).render(<App />);
`,
);
writeFileSync(
  join(dir, "src", "app.tsx"),
  `const App = () => (
  <main style={{ padding: 32, color: "#e6e2d8", font: "16px/1.5 system-ui, sans-serif" }}>
    <h1 style={{ fontWeight: 500 }}>${title.replace(/[{}<>]/g, "")}</h1>
  </main>
);

export { App };
`,
);
writeFileSync(join(dir, "README.md"), `# ${title}\n\n${description}\n`);
console.log(`Created prototypes/${id}/ (prototype.json, src/main.tsx, src/app.tsx, README.md)`);
