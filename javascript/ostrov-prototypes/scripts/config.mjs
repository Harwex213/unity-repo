// The rspack config of one prototype.
// Every prototype is a separate compilation with its own output folder,
// so an error in one prototype never touches the bundle of another one.
import { rspack } from "@rspack/core";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { PROTOTYPES_DIR, ROOT, SHARED_DIR, findEntry, readMeta } from "./lib.mjs";

const ASSET_PATTERN =
  /\.(png|jpe?g|webp|avif|gif|svg|woff2?|ttf|otf|bin|aac|mp3|ogg|wav|m4a|mp4|webm|glb|gltf|hdr|exr|ktx2|wasm)$/i;

// mode: "development" | "production".
// outRoot: the prototype is written to <outRoot>/<id>/.
// publicPath: "/<id>/" for the dev server, "auto" for a static build that works under any base path.
const prototypeConfig = ({ id, mode, outRoot, publicPath = "auto" }) => {
  const meta = readMeta(id);
  const ownTemplate = join(PROTOTYPES_DIR, id, "index.html");
  return {
    name: id,
    mode,
    context: ROOT,
    entry: { main: findEntry(id) },
    output: {
      path: join(outRoot, id),
      publicPath,
      filename: "[name].[contenthash].js",
      cssFilename: "[name].[contenthash].css",
      assetModuleFilename: "assets/[name].[contenthash][ext]",
      clean: true,
    },
    resolve: {
      extensions: ["...", ".ts", ".tsx"],
      alias: { "@shared": SHARED_DIR },
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
        { test: /\.(glsl|vert|frag|txt)$/i, type: "asset/source" },
        { test: ASSET_PATTERN, type: "asset/resource" },
      ],
    },
    plugins: [
      new rspack.HtmlRspackPlugin({
        template: existsSync(ownTemplate) ? ownTemplate : join(SHARED_DIR, "index.html"),
        filename: "index.html",
        title: meta.title,
      }),
    ],
    devtool: mode === "development" ? "cheap-module-source-map" : false,
    performance: { hints: false },
    stats: "errors-warnings",
    infrastructureLogging: { level: "warn" },
  };
};

export { prototypeConfig };
