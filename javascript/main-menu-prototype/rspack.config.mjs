import { rspack } from "@rspack/core";

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
    rules: [
      {
        test: /\.tsx?$/,
        loader: "builtin:swc-loader",
        options: {
          jsc: {
            parser: { syntax: "typescript", tsx: true },
            transform: { react: { runtime: "automatic" } },
          },
        },
        type: "javascript/auto",
      },
      { test: /\.css$/i, type: "css/auto" },
      { test: /\.(png|webp|woff2?|bin)$/i, type: "asset/resource" },
    ],
  },
  plugins: [
    new rspack.HtmlRspackPlugin({ template: "./index.html", filename: "index.html", chunks: ["main"] }),
  ],
  devServer: {
    hot: false,
    liveReload: false,
    // Free port picked by the OS, as in the rest of the repo.
    port: 0,
  },
};
