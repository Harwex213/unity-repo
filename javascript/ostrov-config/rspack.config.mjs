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
    ],
  },
  plugins: [
    new rspack.HtmlRspackPlugin({
      template: "./index.html",
      filename: "index.html",
      chunks: ["main"],
    }),
  ],
  // Ajv and React make one bundle of about 340 KiB.
  // The app is an internal tool, so the limit is raised above the default 250 KiB.
  performance: {
    maxAssetSize: 512 * 1024,
    maxEntrypointSize: 512 * 1024,
  },
  devServer: {
    hot: true,
    // The OS picks a free port. A fixed port lets a forgotten server answer
    // with a stale bundle.
    port: 0,
  },
};
