// `import.meta.webpackContext` of rspack: a static list of modules that match a pattern.
// See src/documents/registry.ts.
type TWebpackContext = {
  keys: () => string[];
  (request: string): unknown;
};

interface ImportMeta {
  webpackContext: (
    request: string,
    options?: { recursive?: boolean; regExp?: RegExp; mode?: "sync" | "eager" | "lazy" | "lazy-once" | "weak" },
  ) => TWebpackContext;
}
