// The dev server of all prototypes on one port.
//
// Usage: npm run dev [-- <id> ...]
//
// - `/` is the list of prototypes, `/<id>/` is one prototype.
// - Each prototype gets its own rspack compiler and its own dev server on a hidden port.
//   This server only routes `/<id>/…` (and the live reload socket `/<id>/ws`) to it.
//   An error or a rebuild of one prototype never reloads or breaks the page of another one.
// - A prototype starts on the first request to its link, so a new prototype folder works
//   without a restart. Ids passed on the command line start at once, and only they are served.
// - The port is free and picked by the OS, unless PORT is set.
import { rspack } from "@rspack/core";
import { RspackDevServer } from "@rspack/dev-server";
import { createServer, request as httpRequest } from "node:http";
import { connect } from "node:net";
import { join } from "node:path";
import { prototypeConfig } from "./config.mjs";
import { ROOT, launcherHtml, listPrototypeIds, listPrototypes, readMeta, requirePrototypeId } from "./lib.mjs";

const pinned = process.argv.slice(2).map(requirePrototypeId);
const isServed = (id) => (pinned.length > 0 ? pinned.includes(id) : listPrototypeIds().includes(id));
const servedPrototypes = () => listPrototypes().filter((item) => isServed(item.id));

// id -> Promise<port of the prototype's own dev server>
const running = new Map();

const startPrototype = async (id) => {
  const meta = readMeta(id);
  const compiler = rspack(
    prototypeConfig({ id, mode: "development", outRoot: join(ROOT, ".cache", "dev"), publicPath: `/${id}/` }),
  );
  compiler.hooks.done.tap("ostrov-prototypes", (stats) => {
    const { errors, warnings } = stats.toJson({ all: false, errors: true, warnings: true });
    const state = errors.length > 0 ? `${errors.length} error(s)` : warnings.length > 0 ? `${warnings.length} warning(s)` : "ok";
    console.log(`[${id}] built: ${state}`);
  });
  const server = new RspackDevServer(
    {
      host: "127.0.0.1",
      port: 0,
      hot: meta.devServer.hot,
      liveReload: meta.devServer.liveReload,
      static: false,
      historyApiFallback: false,
      allowedHosts: "all",
      client: {
        logging: "warn",
        overlay: { errors: true, warnings: false },
        // Host and port of the page itself, the path of this prototype.
        webSocketURL: `auto://0.0.0.0:0/${id}/ws`,
      },
      webSocketServer: { type: "ws", options: { path: `/${id}/ws` } },
    },
    compiler,
  );
  await server.start();
  return server.server.address().port;
};

const ensurePrototype = (id) => {
  if (!running.has(id)) {
    const started = startPrototype(id);
    started.catch((error) => {
      console.error(`[${id}] failed to start: ${error.message}`);
      running.delete(id);
    });
    running.set(id, started);
  }
  return running.get(id);
};

const idOf = (url) => url.split(/[/?#]/)[1] ?? "";

const hub = createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", "http://localhost");
  const id = idOf(url.pathname);
  if (id === "") {
    response.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    response.end(launcherHtml(servedPrototypes(), (item) => `/${item}/`));
    return;
  }
  if (!isServed(id)) {
    response.writeHead(404, { "content-type": "text/plain; charset=utf-8" }).end(`No prototype "${id}"`);
    return;
  }
  if (url.pathname === `/${id}`) {
    response.writeHead(301, { location: `/${id}/${url.search}` }).end();
    return;
  }
  let port;
  try {
    port = await ensurePrototype(id);
  } catch (error) {
    response.writeHead(500, { "content-type": "text/plain; charset=utf-8" }).end(String(error));
    return;
  }
  const upstream = httpRequest(
    { host: "127.0.0.1", port, method: request.method, path: request.url, headers: request.headers },
    (reply) => {
      response.writeHead(reply.statusCode ?? 502, reply.headers);
      reply.pipe(response);
    },
  );
  upstream.on("error", (error) => {
    if (!response.headersSent) {
      response.writeHead(502, { "content-type": "text/plain; charset=utf-8" });
    }
    response.end(String(error));
  });
  request.pipe(upstream);
});

// The live reload socket of a prototype: a raw TCP pipe to its dev server.
hub.on("upgrade", async (request, socket, head) => {
  const id = idOf(request.url ?? "");
  if (!isServed(id)) {
    socket.destroy();
    return;
  }
  let port;
  try {
    port = await ensurePrototype(id);
  } catch {
    socket.destroy();
    return;
  }
  const upstream = connect(port, "127.0.0.1", () => {
    const headers = [];
    for (let index = 0; index < request.rawHeaders.length; index += 2) {
      headers.push(`${request.rawHeaders[index]}: ${request.rawHeaders[index + 1]}`);
    }
    upstream.write(`${request.method} ${request.url} HTTP/${request.httpVersion}\r\n${headers.join("\r\n")}\r\n\r\n`);
    upstream.write(head);
    upstream.pipe(socket);
    socket.pipe(upstream);
  });
  upstream.on("error", () => socket.destroy());
  socket.on("error", () => upstream.destroy());
});

hub.listen(Number(process.env.PORT ?? 0), "127.0.0.1", () => {
  const base = `http://localhost:${hub.address().port}`;
  console.log(`\nOstrov prototypes: ${base}/`);
  for (const item of servedPrototypes()) {
    console.log(`  ${item.title}: ${base}/${item.id}/`);
  }
  console.log("");
  for (const id of pinned) {
    ensurePrototype(id);
  }
});
