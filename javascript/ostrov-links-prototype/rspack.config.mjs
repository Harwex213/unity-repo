import { rspack } from "@rspack/core";
import { boardsMiddleware } from "./scripts/boards.mjs";

export default {
  entry: {
    main: "./src/main.tsx",
  },
  output: {
    filename: "[name].[contenthash].js",
    cssFilename: "[name].[contenthash].css",
    clean: true,
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
        test: /\.(png|jpe?g|webp|svg)$/i,
        type: "asset/resource",
      },
    ],
  },
  plugins: [
    new rspack.HtmlRspackPlugin({
      template: "./index.html",
      filename: "index.html",
      chunks: ["main"],
    }),
    // The build reads the board layouts from data/ next to index.html.
    new rspack.CopyRspackPlugin({
      patterns: [{ from: "data", to: "data" }],
    }),
  ],
  devServer: {
    hot: true,
    // Free port picked by the OS, as everywhere else in the repo: a fixed port
    // lets a forgotten server from an earlier run answer with a stale bundle.
    port: 0,
    // GET and POST /data/<board>.json read and write data/<board>.json.
    // It runs before the copied files, so a reload always sees the file on disk.
    setupMiddlewares: (middlewares) => {
      middlewares.unshift({ name: "boards", middleware: boardsMiddleware(process.cwd()) });
      return middlewares;
    },
  },
  // Saving a board writes data/<board>.json. That write must not rebuild or reload the page.
  watchOptions: {
    ignored: ["**/node_modules/**", "**/data/**"],
  },
};
