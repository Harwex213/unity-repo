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
type TDocument = {
  id: string;
  title: string;
  description: string;
  bounds: TRect;
  draw: (ctx: CanvasRenderingContext2D) => void;
  // Calls `onChange` when the document must be redrawn, for example after an image has loaded.
  // Returns a function that stops the calls.
  subscribe?: (onChange: () => void) => () => void;
};

export type { TDocument, TPoint, TRect };
