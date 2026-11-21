type TPoint = {
  x: number;
  y: number;
};

type TRect = {
  x: number;
  y: number;
  width: number;
  height: number;
};

// A document draws itself in world units.
// The renderer applies the camera transform before the call.
// Each document lives in its own folder `src/documents/<id>/` and exports it as `doc` from `index.ts`.
type TDocument = {
  // Equals the name of the document folder.
  id: string;
  // The place in the sidebar: a lower number stands higher.
  order: number;
  title: string;
  description: string;
  bounds: TRect;
  draw: (ctx: CanvasRenderingContext2D) => void;
  // Calls `onChange` when the document must be redrawn, for example after an image has loaded.
  // Returns a function that stops the calls.
  subscribe?: (onChange: () => void) => () => void;
};

export type { TDocument, TPoint, TRect };
