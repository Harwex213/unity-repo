import { drawTower } from "./glyphs";
import { axialToPoint, hatchHex, traceHex } from "../../kit/hex";
import { FONT_FAMILY } from "../../kit/palette";
import { PALETTE } from "./palette";
import type { TPoint, TRect } from "../../core/types";
import type { TAxial } from "../../kit/hex";

// The cleanup phase of ostrov-prototype: the player steers the own island
// into the enemy hex islands, and the units fight on their own.
// The own island is drawn plain, the enemy islands are hatched.
// Circles are units: blue circles are the player's, red circles are the enemy's.
// The skills bar holds the active skills. The prototype has one: "Разрушить землю" on Q.
// The design adds four more; their glyphs are schematic.

type TIsland = {
  // The island center as a fraction of the area.
  at: TPoint;
  cells: TAxial[];
  // The cells with a unit.
  units: TAxial[];
};

const HEX_SIZE = 16;
const HATCH_STEP = 4.5;
const UNIT_RADIUS = 4.5;

const SKILL_SLOT_SIZE = 26;
const SKILL_SLOT_GAP = 6;
const SKILL_BAR_LEFT = 24;

const FLOWER: TAxial[] = [
  { q: 0, r: 0 },
  { q: 1, r: 0 },
  { q: 1, r: -1 },
  { q: 0, r: -1 },
  { q: -1, r: 0 },
  { q: -1, r: 1 },
  { q: 0, r: 1 },
];

const PLAYER: TIsland = {
  at: { x: 0.2, y: 0.385 },
  cells: FLOWER,
  units: [
    { q: 1, r: 0 },
    { q: 1, r: -1 },
    { q: -1, r: 1 },
    { q: 0, r: 1 },
  ],
};

const ENEMIES: TIsland[] = [
  {
    at: { x: 0.61, y: 0.285 },
    cells: [
      { q: 0, r: 0 },
      { q: 1, r: 0 },
      { q: 0, r: 1 },
      { q: 1, r: -1 },
    ],
    units: [
      { q: 0, r: 0 },
      { q: 0, r: 1 },
      { q: 1, r: -1 },
    ],
  },
  {
    at: { x: 0.848, y: 0.65 },
    cells: [
      { q: 0, r: 0 },
      { q: 1, r: 0 },
      { q: 0, r: -1 },
    ],
    units: [
      { q: 0, r: 0 },
      { q: 1, r: 0 },
    ],
  },
];

const islandOrigin = (island: TIsland, area: TRect): TPoint => ({
  x: area.x + area.width * island.at.x,
  y: area.y + area.height * island.at.y,
});

const cellCenter = (origin: TPoint, cell: TAxial): TPoint => {
  const point = axialToPoint(cell, HEX_SIZE);
  return { x: origin.x + point.x, y: origin.y + point.y };
};

const drawUnit = (ctx: CanvasRenderingContext2D, center: TPoint, isEnemy: boolean) => {
  ctx.beginPath();
  ctx.arc(center.x, center.y, UNIT_RADIUS, 0, Math.PI * 2);
  ctx.fillStyle = isEnemy ? PALETTE.enemy : PALETTE.ally;
  ctx.fill();
  // A paper ring keeps a unit apart from the hatching under it.
  ctx.strokeStyle = PALETTE.paper;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.strokeStyle = PALETTE.ink;
};

const drawIsland = (ctx: CanvasRenderingContext2D, island: TIsland, area: TRect, isEnemy: boolean) => {
  const origin = islandOrigin(island, area);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineJoin = "round";
  for (const cell of island.cells) {
    const center = cellCenter(origin, cell);
    if (isEnemy) {
      hatchHex(ctx, center, HEX_SIZE, HATCH_STEP);
    }
    traceHex(ctx, center, HEX_SIZE);
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }
  for (const cell of island.units) {
    drawUnit(ctx, cellCenter(origin, cell), isEnemy);
  }
};

const drawCourse = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint) => {
  ctx.setLineDash([5, 4]);
  ctx.beginPath();
  ctx.moveTo(from.x, from.y);
  ctx.lineTo(to.x, to.y);
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.setLineDash([]);
  const angle = Math.atan2(to.y - from.y, to.x - from.x);
  ctx.beginPath();
  ctx.moveTo(to.x - 9 * Math.cos(angle - 0.45), to.y - 9 * Math.sin(angle - 0.45));
  ctx.lineTo(to.x, to.y);
  ctx.lineTo(to.x - 9 * Math.cos(angle + 0.45), to.y - 9 * Math.sin(angle + 0.45));
  ctx.stroke();
};

const skillSlotRect = (area: TRect, index: number): TRect => ({
  x: area.x + SKILL_BAR_LEFT + index * (SKILL_SLOT_SIZE + SKILL_SLOT_GAP),
  // The space under the bar holds the head of the arrow that points at a slot.
  y: area.y + area.height - SKILL_SLOT_SIZE - 14,
  width: SKILL_SLOT_SIZE,
  height: SKILL_SLOT_SIZE,
});

// The glyph of "Разрушить землю" in the prototype: a hex split by a crack.
const drawShatterGlyph = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  traceHex(ctx, center, size);
  ctx.lineWidth = 1.25;
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(center.x + size * 0.15, center.y - size);
  ctx.lineTo(center.x - size * 0.15, center.y - size * 0.3);
  ctx.lineTo(center.x + size * 0.2, center.y + size * 0.05);
  ctx.lineTo(center.x - size * 0.2, center.y + size * 0.5);
  ctx.lineTo(center.x + size * 0.05, center.y + size);
  ctx.stroke();
};

const drawDot = (ctx: CanvasRenderingContext2D, center: TPoint, radius: number, color: string) => {
  ctx.beginPath();
  ctx.arc(center.x, center.y, radius, 0, Math.PI * 2);
  ctx.fillStyle = color;
  ctx.fill();
};

// A vertical arrow. `direction` is -1 for up and 1 for down.
const drawSmallArrow = (ctx: CanvasRenderingContext2D, center: TPoint, half: number, direction: number) => {
  const tip = { x: center.x, y: center.y + half * direction };
  const head = half * 0.6;
  ctx.beginPath();
  ctx.moveTo(center.x, center.y - half * direction);
  ctx.lineTo(tip.x, tip.y);
  ctx.moveTo(tip.x - head, tip.y - head * direction);
  ctx.lineTo(tip.x, tip.y);
  ctx.lineTo(tip.x + head, tip.y - head * direction);
  ctx.stroke();
};

// An army buff or debuff: a unit and an arrow next to it.
const drawUnitChange = (ctx: CanvasRenderingContext2D, center: TPoint, size: number, color: string, direction: number) => {
  drawDot(ctx, { x: center.x - size * 0.35, y: center.y }, size * 0.45, color);
  ctx.lineWidth = 1.25;
  drawSmallArrow(ctx, { x: center.x + size * 0.55, y: center.y }, size * 0.6, direction);
};

// Damage to the enemy army: an enemy unit and a lightning bolt.
const drawStrikeGlyph = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  drawDot(ctx, { x: center.x + size * 0.35, y: center.y + size * 0.3 }, size * 0.45, PALETTE.enemy);
  ctx.beginPath();
  ctx.moveTo(center.x - size * 0.2, center.y - size * 0.9);
  ctx.lineTo(center.x - size * 0.65, center.y - size * 0.05);
  ctx.lineTo(center.x - size * 0.2, center.y - size * 0.05);
  ctx.lineTo(center.x - size * 0.55, center.y + size * 0.8);
  ctx.lineWidth = 1.25;
  ctx.stroke();
};

// A gambling event: a die. The outcome can help either army.
const drawDiceGlyph = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const half = size * 0.75;
  ctx.beginPath();
  ctx.roundRect(center.x - half, center.y - half, half * 2, half * 2, size * 0.2);
  ctx.lineWidth = 1.25;
  ctx.stroke();
  const pip = size * 0.13;
  const step = half * 0.5;
  drawDot(ctx, { x: center.x - step, y: center.y - step }, pip, PALETTE.ink);
  drawDot(ctx, center, pip, PALETTE.ink);
  drawDot(ctx, { x: center.x + step, y: center.y + step }, pip, PALETTE.ink);
};

type TSkill = {
  name: string;
  detail?: string;
  drawGlyph: (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => void;
};

const SKILLS: TSkill[] = [
  { name: "разрушить гекс", drawGlyph: drawShatterGlyph },
  { name: "усилить армию", drawGlyph: (ctx, center, size) => drawUnitChange(ctx, center, size, PALETTE.ally, -1) },
  {
    name: "ослабить вражескую армию",
    drawGlyph: (ctx, center, size) => drawUnitChange(ctx, center, size, PALETTE.enemy, 1),
  },
  { name: "нанести вражеской армии урон", drawGlyph: drawStrikeGlyph },
  { name: "запустить гэмблинг ивент", detail: "польза своей или вражеской армии", drawGlyph: drawDiceGlyph },
];

const SKILL_GLYPH_SIZE = 8;

// A skill slot with the glyph of the skill.
const drawSkillSlot = (ctx: CanvasRenderingContext2D, slot: TRect, skill: TSkill) => {
  ctx.beginPath();
  ctx.roundRect(slot.x, slot.y, slot.width, slot.height, 4);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.lineJoin = "round";
  ctx.lineCap = "round";
  skill.drawGlyph(ctx, { x: slot.x + slot.width / 2, y: slot.y + slot.height / 2 }, SKILL_GLYPH_SIZE);
};

// The first slot keeps the key of the prototype skill. The prototype has no keys for the other skills.
const drawSkillsBar = (ctx: CanvasRenderingContext2D, area: TRect) => {
  SKILLS.forEach((skill, index) => drawSkillSlot(ctx, skillSlotRect(area, index), skill));

  const first = skillSlotRect(area, 0);
  ctx.font = `600 7px ${FONT_FAMILY}`;
  ctx.fillStyle = PALETTE.ink;
  ctx.textAlign = "right";
  ctx.textBaseline = "alphabetic";
  ctx.fillText("Q", first.x + first.width - 2.5, first.y + first.height - 2.5);
};

// The battle is drawn in a base frame of this height and scaled up to the phase area.
const BASE_HEIGHT = 182;

type TBattleFrame = {
  scale: number;
  // The area in base units, with its top left corner at the origin.
  base: TRect;
};

const battleFrame = (area: TRect): TBattleFrame => {
  const scale = area.height / BASE_HEIGHT;
  return { scale, base: { x: 0, y: 0, width: area.width / scale, height: BASE_HEIGHT } };
};

// The bottom middle of the skills bar, where an arrow from below ends. `gap` is in world units.
const battleSkillTip = (area: TRect, gap: number): TPoint => {
  const { scale, base } = battleFrame(area);
  const first = skillSlotRect(base, 0);
  const last = skillSlotRect(base, SKILLS.length - 1);
  return {
    x: area.x + ((first.x + last.x + last.width) / 2) * scale,
    y: area.y + (first.y + first.height) * scale + gap,
  };
};

const LEGEND_ROW_HEIGHT = 36;

// The legend of the skills bar: each skill slot with the name of the skill at its right.
// `at` is the top left corner of the first slot.
const drawSkillsLegend = (ctx: CanvasRenderingContext2D, at: TPoint) => {
  SKILLS.forEach((skill, index) => {
    const slot = { x: at.x, y: at.y + index * LEGEND_ROW_HEIGHT, width: SKILL_SLOT_SIZE, height: SKILL_SLOT_SIZE };
    drawSkillSlot(ctx, slot, skill);
    const textX = slot.x + SKILL_SLOT_SIZE + 10;
    const middle = slot.y + SKILL_SLOT_SIZE / 2;
    ctx.textAlign = "left";
    ctx.textBaseline = "middle";
    ctx.fillStyle = PALETTE.ink;
    ctx.font = `400 15px ${FONT_FAMILY}`;
    if (skill.detail) {
      ctx.fillText(skill.name, textX, middle - 7);
      ctx.font = `400 13px ${FONT_FAMILY}`;
      ctx.fillStyle = PALETTE.muted;
      ctx.fillText(skill.detail, textX, middle + 10);
    } else {
      ctx.fillText(skill.name, textX, middle);
    }
  });
};

const drawBattleIslands = (ctx: CanvasRenderingContext2D, area: TRect) => {
  const { scale, base } = battleFrame(area);
  ctx.save();
  ctx.translate(area.x, area.y);
  ctx.scale(scale, scale);
  drawBattleBase(ctx, base);
  ctx.restore();
};

const drawBattleBase = (ctx: CanvasRenderingContext2D, area: TRect) => {
  for (const enemy of ENEMIES) {
    drawIsland(ctx, enemy, area, true);
  }
  drawIsland(ctx, PLAYER, area, false);
  drawTower(ctx, islandOrigin(PLAYER, area), HEX_SIZE * 0.9);

  // The course of the own island towards the first enemy.
  drawCourse(
    ctx,
    { x: area.x + area.width * 0.36, y: area.y + area.height * 0.37 },
    { x: area.x + area.width * 0.53, y: area.y + area.height * 0.3 },
  );

  drawSkillsBar(ctx, area);
};

export { battleSkillTip, drawBattleIslands, drawSkillsLegend };
