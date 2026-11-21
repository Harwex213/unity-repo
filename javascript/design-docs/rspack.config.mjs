import { rspack } from "@rspack/core";
import { NOTES_FILE, notesMiddleware, readAllNotes } from "./scripts/notes-store.mjs";

// Snapshot builds (scripts/snapshot.mjs) set two variables:
// - DOC_ONLY: the id of the one document to bundle. Other documents stay out of the build,
//   so a broken document does not break the snapshot of another one.
// - DOCS_OUT_DIR: the output folder, so parallel builds do not overwrite each other.
const docOnly = process.env.DOC_ONLY;
const outDir = process.env.DOCS_OUT_DIR;

if (docOnly && !/^[a-z0-9-]+$/.test(docOnly)) {
  throw new Error(`DOC_ONLY must be a document folder name, got "${docOnly}"`);
}

const docOnlyPlugins = docOnly
  ? [new rspack.ContextReplacementPlugin(/[\\/]src[\\/]documents$/, new RegExp(`^\\./${docOnly}/index\\.ts$`))]
  : [];

export default {
  entry: {
    main: "./src/main.tsx",
  },
  output: {
    filename: "[name].[contenthash].js",
    cssFilename: "[name].[contenthash].css",
    clean: true,
    ...(outDir ? { path: outDir } : {}),
  },
  resolve: {
    extensions: ["...", ".ts", ".tsx"],
  },
  module: {
    parser: {
      "css/auto": {
        namedExports: false,
      },
    },
    rules: [
      {
        test: /\.tsx?$/,
        loader: "builtin:swc-loader",
        options: {
          jsc: {
            parser: {
              syntax: "typescript",
              tsx: true,
            },
            transform: {
              react: {
                runtime: "automatic",
              },
            },
          },
        },
        type: "javascript/auto",
      },
      {
        test: /\.css$/i,
        type: "css/auto",
      },
      {
        test: /\.(png|jpe?g|webp)$/i,
        type: "asset/resource",
      },
    ],
  },
  plugins: [
    ...docOnlyPlugins,
    // The notes at build time. The dev server serves fresh notes at /api/notes/<id> (scripts/notes-store.mjs).
    new rspack.DefinePlugin({ __DOC_NOTES__: `(${JSON.stringify(readAllNotes())})` }),
    new rspack.HtmlRspackPlugin({
      template: "./index.html",
      filename: "index.html",
      chunks: ["main"],
    }),
  ],
  performance: {
    // Document art is large on purpose; the viewer is a local tool.
    hints: false,
  },
  // The viewer writes notes while the dev server runs. A rebuild on each save would reload the page.
  watchOptions: {
    ignored: [`**/${NOTES_FILE}`, `**/${NOTES_FILE}.tmp`],
  },
  devServer: {
    hot: true,
    setupMiddlewares: (middlewares) => [notesMiddleware, ...middlewares],
    // The OS picks a free port. A fixed port lets a forgotten server answer
    // with a stale bundle.
    port: 0,
  },
};
