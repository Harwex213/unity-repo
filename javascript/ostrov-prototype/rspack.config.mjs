import { rspack } from "@rspack/core";
import { storeSnapshotMiddleware } from "./dev-middleware/store-snapshot-middleware.mjs";

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
  ],
  devServer: {
    hot: true,
    // Free port picked by the OS, as everywhere else in the repo: a fixed port
    // lets a forgotten server from an earlier run answer with a stale bundle.
    port: 0,
    setupMiddlewares: (middlewares) => {
      middlewares.unshift(storeSnapshotMiddleware);

      return middlewares;
    },
  },
};
