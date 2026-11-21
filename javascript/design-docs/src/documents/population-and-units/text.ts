import { FONT_FAMILY } from "../../kit/palette";
import { drawText } from "../../kit/shapes";
import type { TTextOptions } from "../../kit/shapes";

// Text that wraps by words inside a given width.

// Splits the text into lines no wider than `width`, by words.
// A no-break space keeps two words on one line.
const wrapLines = (ctx: CanvasRenderingContext2D, text: string, width: number, options: TTextOptions = {}): string[] => {
  ctx.font = `${options.weight ?? 400} ${options.size ?? 16}px ${options.family ?? FONT_FAMILY}`;
  const lines: string[] = [];
  let line = "";
  for (const word of text.split(" ")) {
    const candidate = line ? `${line} ${word}` : word;
    if (line && ctx.measureText(candidate).width > width) {
      lines.push(line);
      line = word;
    } else {
      line = candidate;
    }
  }
  if (line) {
    lines.push(line);
  }
  return lines;
};

const lineHeightOf = (options: TTextOptions) => options.lineHeight ?? Math.round((options.size ?? 16) * 1.35);

// Draws the wrapped text with its top at `y`. Returns the bottom of the text.
const drawWrappedText = (
  ctx: CanvasRenderingContext2D,
  text: string,
  x: number,
  y: number,
  width: number,
  options: TTextOptions = {},
): number => {
  const lines = wrapLines(ctx, text, width, options);
  drawText(ctx, lines.join("\n"), x, y, options);
  return y + lines.length * lineHeightOf(options);
};

export { drawWrappedText, lineHeightOf, wrapLines };
