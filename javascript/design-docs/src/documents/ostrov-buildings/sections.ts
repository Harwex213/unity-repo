import { drawImageContain } from "../../kit/image";
import { drawText, drawTextCenteredAt, drawTextInRect } from "../../kit/shapes";
import { BUILDING_ART, ICONS } from "./art";
import { PALETTE } from "./palette";
import { STYLE, bottomOf, centerXOf, circle, hex, line, middleYOf, rect, rightOf, squareAround } from "./wire";
import type { TImageAsset } from "../../kit/image";
import type { TPoint, TRect } from "../../core/types";
import type { TStyle } from "./wire";

// The sections of the selected building panel, as the hex card of ostrov-prototype
// (src/ui/components/hex-modal.tsx) shows the village.
// The panel of the HUD and block 5 draw the same sections.
// A tone sets the colors: the bright tone for the part the document is about,
// the dim tone for the rest. The dim tone keeps the icons in color, but faint.

type TTone = {
  frame: TStyle;
  chip: TStyle;
  dashed: TStyle;
  button: TStyle;
  emblem: TStyle;
  hp: TStyle;
  text: string;
  note: string;
  positive: string;
  negative: string;
  iconAlpha: number;
};

const BRIGHT_TONE: TTone = {
  frame: STYLE.section,
  chip: STYLE.chip,
  dashed: STYLE.dashed,
  button: STYLE.button,
  emblem: { fill: PALETTE.soft, stroke: PALETTE.ink, width: 2 },
  hp: { fill: PALETTE.hp, stroke: PALETTE.ink, width: 1 },
  text: PALETTE.ink,
  note: PALETTE.muted,
  positive: PALETTE.positive,
  negative: PALETTE.negative,
  iconAlpha: 1,
};

const DIM_TONE: TTone = {
  frame: STYLE.dimPanel,
  chip: STYLE.dimPanel,
  dashed: { fill: PALETTE.paper, stroke: PALETTE.dim, width: 1.5, dash: [5, 4] },
  button: STYLE.dimLine,
  emblem: { fill: PALETTE.paper, stroke: PALETTE.dim, width: 2 },
  hp: { fill: PALETTE.dim, stroke: PALETTE.dim, width: 1 },
  text: PALETTE.dimText,
  note: PALETTE.dimText,
  positive: PALETTE.dimText,
  negative: PALETTE.dim,
  iconAlpha: 0.3,
};

// The width of a section: the panel width without its padding.
const SECTION_WIDTH = 383;

const SECTION_HEIGHT = {
  header: 96,
  dice: 142,
  village: 132,
  modules: 114,
  workers: 84,
  population: 96,
  resources: 198,
  passiveDefense: 84,
  activeDefense: 100,
};

const INSET = 12;
const SECTION_BODY_TOP = 36;

// The small caps label of a section with a thin line to the right edge.
const drawSectionTitle = (ctx: CanvasRenderingContext2D, area: TRect, title: string, tone: TTone) => {
  const top = area.y + INSET;
  drawText(ctx, title, area.x + INSET, top, { size: 13, weight: 700, color: tone.text });
  const width = ctx.measureText(title).width;
  const y = top + 9;
  line(ctx, { x: area.x + INSET + width + 10, y }, { x: rightOf(area) - INSET, y }, { stroke: PALETTE.dim, width: 1 });
};

const drawIcon = (ctx: CanvasRenderingContext2D, icon: TImageAsset, area: TRect, tone: TTone) => {
  ctx.save();
  ctx.globalAlpha = tone.iconAlpha;
  drawImageContain(ctx, icon, area);
  ctx.restore();
};

// An icon and a number, side by side. `x` is the left edge of the icon.
const ICON_SIDE = 22;
const drawIconValue = (
  ctx: CanvasRenderingContext2D,
  icon: TImageAsset,
  value: string,
  point: TPoint,
  tone: TTone,
  color: string = tone.text,
) => {
  drawIcon(ctx, icon, squareAround({ x: point.x + ICON_SIDE / 2, y: point.y }, ICON_SIDE), tone);
  drawTextCenteredAt(ctx, value, point.x + ICON_SIDE + 5, point.y, { size: 18, weight: 600, color });
};

// A value in a box at the right edge of a row, as the buttons of the prototype.
const drawValueBox = (ctx: CanvasRenderingContext2D, area: TRect, y: number, value: string, tone: TTone) => {
  const box: TRect = { x: rightOf(area) - INSET - 60, y: y - 17, width: 60, height: 34 };
  rect(ctx, box, 6, tone.chip);
  drawTextInRect(ctx, value, box, { size: 18, weight: 700, color: tone.text });
};

// ---------- Header ----------

// The emblem: the biome hex with the art of the building. The header has no frame, as in the prototype.
const drawHeader = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  const emblem: TPoint = { x: area.x + 44, y: middleYOf(area) };
  hex(ctx, emblem, 42, tone.emblem);
  drawIcon(ctx, BUILDING_ART.village, squareAround({ x: emblem.x, y: emblem.y - 2 }, 66), tone);

  const textX = area.x + 100;
  drawText(ctx, "Деревня", textX, area.y + 8, { size: 22, weight: 700, color: tone.text });
  drawText(ctx, "Умеренные луга", textX, area.y + 38, { size: 14, color: tone.note });

  const bar: TRect = { x: textX, y: area.y + 70, width: 140, height: 8 };
  rect(ctx, bar, 4, tone.hp);
  drawTextCenteredAt(ctx, "150/150", rightOf(bar) + 10, middleYOf(bar), { size: 14, color: tone.text });

  // The demolish button and the close cross.
  const demolish: TRect = { x: rightOf(area) - 44, y: area.y + 56, width: 36, height: 32 };
  rect(ctx, demolish, 6, tone.button);
  drawIcon(ctx, ICONS.demolish, squareAround({ x: centerXOf(demolish), y: middleYOf(demolish) }, 22), tone);
  const cross: TPoint = { x: rightOf(area) - 14, y: area.y + 12 };
  const arm = 8;
  const crossStyle = { stroke: tone.negative, width: 3 };
  line(ctx, { x: cross.x - arm, y: cross.y - arm }, { x: cross.x + arm, y: cross.y + arm }, crossStyle);
  line(ctx, { x: cross.x + arm, y: cross.y - arm }, { x: cross.x - arm, y: cross.y + arm }, crossStyle);
};

// ---------- Dice ----------

// The faces of the village die: population and contamination. The last face is the face of the biome.
type TFace = { population: string; toxicity: string };
const FACES: TFace[] = [
  { population: "1", toxicity: "0" },
  { population: "2", toxicity: "2" },
  { population: "3", toxicity: "3" },
  { population: "1", toxicity: "1" },
];
const BIOME_FACE: TFace = { population: "3", toxicity: "1" };
const CHIP = { height: 34, gap: 8 };

const chipWidthOf = (area: TRect) => (area.width - INSET * 2 - CHIP.gap) / 2;

const drawFace = (ctx: CanvasRenderingContext2D, chip: TRect, face: TFace, tone: TTone) => {
  const y = middleYOf(chip);
  drawIconValue(ctx, ICONS.population, face.population, { x: centerXOf(chip) - 46, y }, tone);
  drawIconValue(ctx, ICONS.toxicity, face.toxicity, { x: centerXOf(chip) + 8, y }, tone, tone.positive);
};

// The chips of the faces, two per row, and the face of the biome centered under them.
const drawFaces = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  const chipWidth = chipWidthOf(area);
  FACES.forEach((face, index) => {
    const chip: TRect = {
      x: area.x + INSET + (index % 2) * (chipWidth + CHIP.gap),
      y: area.y + INSET + Math.floor(index / 2) * (CHIP.height + CHIP.gap),
      width: chipWidth,
      height: CHIP.height,
    };
    rect(ctx, chip, 5, tone.chip);
    drawFace(ctx, chip, face, tone);
  });
  const biomeChip: TRect = {
    x: centerXOf(area) - chipWidth / 2,
    y: area.y + INSET + (CHIP.height + CHIP.gap) * 2,
    width: chipWidth,
    height: CHIP.height,
  };
  rect(ctx, biomeChip, 5, tone.dashed);
  drawFace(ctx, biomeChip, BIOME_FACE, tone);
};

const drawDice = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawFaces(ctx, area, tone);
};

// ---------- Resources ----------

// The faces of the die, as the dice section, and under a thin line the fixed income of every turn:
// a resource and the growth of contamination.
const INCOME: TFace = { population: "+1", toxicity: "+1" };

const drawResources = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawFaces(ctx, area, tone);
  const ruleY = area.y + SECTION_HEIGHT.dice;
  line(ctx, { x: area.x + INSET, y: ruleY }, { x: rightOf(area) - INSET, y: ruleY }, { stroke: PALETTE.dim, width: 1 });
  const chipWidth = chipWidthOf(area);
  const chip: TRect = { x: rightOf(area) - INSET - chipWidth, y: ruleY + 10, width: chipWidth, height: CHIP.height };
  drawTextCenteredAt(ctx, "каждый ход:", area.x + INSET, middleYOf(chip), { size: 16, weight: 500, color: tone.text });
  rect(ctx, chip, 5, tone.chip);
  drawFace(ctx, chip, INCOME, tone);
};

// ---------- Village ----------

const drawVillage = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawSectionTitle(ctx, area, "ДЕРЕВНЯ", tone);
  const button: TRect = { x: area.x + INSET, y: area.y + SECTION_BODY_TOP, width: area.width - INSET * 2, height: 36 };
  rect(ctx, button, 6, tone.button);
  drawTextInRect(ctx, "▶ Запустить", button, { size: 16, weight: 500, color: tone.text });
  drawText(ctx, "Сама кубик не бросает. Без запуска не даёт\nжителей", area.x + INSET, bottomOf(button) + 8, {
    size: 14,
    lineHeight: 19,
    color: tone.note,
  });
};

// ---------- Modules ----------

// Module slots of the building: one holds a module, the others are free.
// A free slot has a plus: the player adds a module there.
const MODULE_SLOTS = 3;
const SLOT = { side: 60, gap: 10 };

// A small annex: a box with a lean-to roof and a door, as the module glyph of ostrov-core-cycle.
const drawModuleGlyph = (ctx: CanvasRenderingContext2D, center: TPoint, side: number, tone: TTone) => {
  const half = side / 2;
  const bottom = center.y + half * 0.8;
  ctx.save();
  ctx.beginPath();
  ctx.moveTo(center.x - half, bottom);
  ctx.lineTo(center.x - half, center.y - half * 0.2);
  ctx.lineTo(center.x + half, center.y - half * 0.8);
  ctx.lineTo(center.x + half, bottom);
  ctx.closePath();
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = tone.text;
  ctx.lineWidth = 1.5;
  ctx.lineJoin = "round";
  ctx.stroke();
  ctx.restore();
  const door = { x: center.x - half * 0.2, y: bottom - half * 0.7, width: half * 0.4, height: half * 0.7 };
  rect(ctx, door, 0, { stroke: tone.text, width: 1.5 });
};

const drawPlus = (ctx: CanvasRenderingContext2D, center: TPoint, arm: number, tone: TTone) => {
  const style = { stroke: tone.text, width: 2 };
  line(ctx, { x: center.x - arm, y: center.y }, { x: center.x + arm, y: center.y }, style);
  line(ctx, { x: center.x, y: center.y - arm }, { x: center.x, y: center.y + arm }, style);
};

const drawModules = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawSectionTitle(ctx, area, "МОДУЛИ", tone);
  for (let index = 0; index < MODULE_SLOTS; index++) {
    const slot: TRect = {
      x: area.x + INSET + index * (SLOT.side + SLOT.gap),
      y: area.y + SECTION_BODY_TOP,
      width: SLOT.side,
      height: SLOT.side,
    };
    const center = { x: centerXOf(slot), y: middleYOf(slot) };
    if (index === 0) {
      rect(ctx, slot, 6, tone.button);
      drawModuleGlyph(ctx, center, SLOT.side * 0.55, tone);
    } else {
      rect(ctx, slot, 6, tone.dashed);
      drawPlus(ctx, center, 10, tone);
    }
  }
};

// ---------- Workers ----------

// The middle of the single row of a section with a title.
const rowMiddleOf = (area: TRect) => area.y + SECTION_BODY_TOP + 18;

const drawWorkers = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawSectionTitle(ctx, area, "РАБОЧИЕ", tone);
  const y = rowMiddleOf(area);
  const x = area.x + INSET;
  drawIcon(ctx, ICONS.population, squareAround({ x: x + 12, y }, 24), tone);
  const status = "1/1 — ";
  drawTextCenteredAt(ctx, status, x + 32, y, { size: 18, weight: 500, color: tone.text });
  const statusWidth = ctx.measureText(status).width;
  drawTextCenteredAt(ctx, "работает", x + 32 + statusWidth, y, { size: 18, weight: 700, color: tone.positive });
  const button: TRect = { x: rightOf(area) - INSET - 76, y: y - 17, width: 76, height: 34 };
  rect(ctx, button, 6, tone.button);
  drawTextInRect(ctx, "Снять", button, { size: 15, weight: 600, color: tone.text });
};

// ---------- Population ----------

// How many people live in the building and how many it holds.
// A bar of places under the line: a filled place is a resident.
const POPULATION = { lives: 3, holds: 5 };
const PLACE = { height: 12, gap: 6 };

const drawPopulation = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawSectionTitle(ctx, area, "НАСЕЛЕНИЕ", tone);
  const y = area.y + SECTION_BODY_TOP + 14;
  const x = area.x + INSET;
  drawIcon(ctx, ICONS.population, squareAround({ x: x + 12, y }, 24), tone);
  drawTextCenteredAt(ctx, `живёт ${POPULATION.lives} / вмещает ${POPULATION.holds}`, x + 32, y, {
    size: 18,
    weight: 500,
    color: tone.text,
  });
  const placeWidth = (area.width - INSET * 2 - PLACE.gap * (POPULATION.holds - 1)) / POPULATION.holds;
  for (let index = 0; index < POPULATION.holds; index++) {
    const place: TRect = { x: x + index * (placeWidth + PLACE.gap), y: y + 22, width: placeWidth, height: PLACE.height };
    rect(ctx, place, 3, index < POPULATION.lives ? { ...tone.chip, fill: tone.note } : tone.chip);
  }
};

// ---------- Defense ----------

// A mine: a round body with four short spikes.
const drawMineGlyph = (ctx: CanvasRenderingContext2D, center: TPoint, tone: TTone) => {
  const radius = 8;
  const spike = 13;
  const style = { stroke: tone.text, width: 2 };
  line(ctx, { x: center.x - spike, y: center.y }, { x: center.x + spike, y: center.y }, style);
  line(ctx, { x: center.x, y: center.y - spike }, { x: center.x, y: center.y + spike }, style);
  circle(ctx, center, radius, { fill: tone.text });
};

// Passive defense: the mines of the building and the attack level of a mine.
const drawPassiveDefense = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawSectionTitle(ctx, area, "ПАССИВНАЯ ОБОРОНА", tone);
  const y = rowMiddleOf(area);
  const x = area.x + INSET;
  drawMineGlyph(ctx, { x: x + 13, y }, tone);
  drawTextCenteredAt(ctx, "уровень атаки мин", x + 36, y, { size: 18, weight: 500, color: tone.text });
  drawValueBox(ctx, area, y, "2", tone);
};

// Active defense: three values of the attack side by side.
const ATTACK_STATS = [
  { label: "уровень атаки", value: "3" },
  { label: "скорость атаки", value: "1" },
  { label: "дальность атаки", value: "2" },
];
const STAT = { height: 52, gap: 8 };

const drawActiveDefense = (ctx: CanvasRenderingContext2D, area: TRect, tone: TTone) => {
  rect(ctx, area, 8, tone.frame);
  drawSectionTitle(ctx, area, "АКТИВНАЯ ОБОРОНА", tone);
  const width = (area.width - INSET * 2 - STAT.gap * (ATTACK_STATS.length - 1)) / ATTACK_STATS.length;
  ATTACK_STATS.forEach((stat, index) => {
    const chip: TRect = {
      x: area.x + INSET + index * (width + STAT.gap),
      y: area.y + SECTION_BODY_TOP,
      width,
      height: STAT.height,
    };
    rect(ctx, chip, 5, tone.chip);
    drawText(ctx, stat.label, centerXOf(chip), chip.y + 7, { size: 12, color: tone.note, align: "center" });
    drawText(ctx, stat.value, centerXOf(chip), chip.y + 24, { size: 20, weight: 700, color: tone.text, align: "center" });
  });
};

export {
  BRIGHT_TONE,
  DIM_TONE,
  SECTION_HEIGHT,
  SECTION_WIDTH,
  drawActiveDefense,
  drawDice,
  drawHeader,
  drawModules,
  drawPassiveDefense,
  drawPopulation,
  drawResources,
  drawVillage,
  drawWorkers,
};
export type { TTone };
