// Imports of files that the bundler turns into URLs (see ASSET_PATTERN in scripts/config.mjs).
declare module "*.png" { const src: string; export default src; }
declare module "*.jpg" { const src: string; export default src; }
declare module "*.jpeg" { const src: string; export default src; }
declare module "*.webp" { const src: string; export default src; }
declare module "*.avif" { const src: string; export default src; }
declare module "*.gif" { const src: string; export default src; }
declare module "*.svg" { const src: string; export default src; }
declare module "*.bin" { const src: string; export default src; }
declare module "*.aac" { const src: string; export default src; }
declare module "*.mp3" { const src: string; export default src; }
declare module "*.ogg" { const src: string; export default src; }
declare module "*.wav" { const src: string; export default src; }
declare module "*.m4a" { const src: string; export default src; }
declare module "*.mp4" { const src: string; export default src; }
declare module "*.webm" { const src: string; export default src; }
declare module "*.glb" { const src: string; export default src; }
declare module "*.gltf" { const src: string; export default src; }
declare module "*.hdr" { const src: string; export default src; }
declare module "*.exr" { const src: string; export default src; }
declare module "*.ktx2" { const src: string; export default src; }
declare module "*.wasm" { const src: string; export default src; }
// Imports of text files as strings.
declare module "*.glsl" { const source: string; export default source; }
declare module "*.vert" { const source: string; export default source; }
declare module "*.frag" { const source: string; export default source; }
declare module "*.txt" { const source: string; export default source; }
declare module "*.css";
declare module "@fontsource/*";
