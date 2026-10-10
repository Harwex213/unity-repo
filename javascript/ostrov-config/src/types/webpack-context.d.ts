// Rspack replaces import.meta.webpackContext at build time.
// The call returns a function that loads one matched module by its key.
type TWebpackContext = {
  (key: string): unknown;
  keys: () => string[];
};

type TWebpackContextOptions = {
  recursive?: boolean;
  regExp?: RegExp;
};

interface ImportMeta {
  webpackContext: (request: string, options?: TWebpackContextOptions) => TWebpackContext;
}
