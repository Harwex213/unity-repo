import type { TRect } from "../core/types";

// An image loads after the first frame.
// A document passes `subscribe` to the viewer, so the viewer redraws on load.

type TImageAsset = {
  image: HTMLImageElement;
  subscribe: (onLoad: () => void) => () => void;
};

const createImageAsset = (src: string): TImageAsset => {
  const image = new Image();
  const listeners = new Set<() => void>();
  image.addEventListener("load", () => {
    for (const listener of listeners) {
      listener();
    }
  });
  image.src = src;

  return {
    image,
    subscribe: (onLoad) => {
      listeners.add(onLoad);
      return () => {
        listeners.delete(onLoad);
      };
    },
  };
};

// The whole image fits in the rect and keeps its aspect. It stands in the center of the rect.
// Nothing is drawn until the image has loaded.
const drawImageContain = (ctx: CanvasRenderingContext2D, asset: TImageAsset, rect: TRect) => {
  const { image } = asset;
  if (!image.complete || image.naturalWidth === 0) {
    return;
  }
  const scale = Math.min(rect.width / image.naturalWidth, rect.height / image.naturalHeight);
  const width = image.naturalWidth * scale;
  const height = image.naturalHeight * scale;
  ctx.imageSmoothingQuality = "high";
  ctx.drawImage(image, rect.x + (rect.width - width) / 2, rect.y + (rect.height - height) / 2, width, height);
};

export { createImageAsset, drawImageContain };
export type { TImageAsset };
