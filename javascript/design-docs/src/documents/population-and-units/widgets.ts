import { traceHex } from "../../kit/hex";
import { drawImageContain } from "../../kit/image";
import { FONT_FAMILY } from "../../kit/palette";
import { drawText, drawTextCenteredAt, drawTextInRect } from "../../kit/shapes";
import { BUILDING_ART, ICON_ART } from "./art";
import { drawResourceCell } from "./hud";
import { drawBuildingOnHex } from "./island";
import { PALETTE } from "./palette";
import { drawButton, drawPanel, drawSectionHeader } from "./primitives";
import { drawWrappedText } from "./text";
import { CARD_PLATE, drawWorkersPlate } from "./workers-plate";
import type { TBuildingId, TIconId } from "./art";
import type { THudState } from "./hud";
import type { TPoint, TRect } from "../../core/types";

// Wireframes of the prototype's interface, from the user's screenshots and ostrov-prototype.
// Every widget has a fixed size and draws from its top left corner.

const drawIcon = (ctx: CanvasRenderingContext2D, id: TIconId, x: number, y: number, size: number) => {
  drawImageContain(ctx, ICON_ART[id], { x, y, width: size, height: size });
};

const textWidth = (ctx: CanvasRenderingContext2D, text: string, size: number, weight = 400) => {
  ctx.font = `${weight} ${size}px ${FONT_FAMILY}`;
  return ctx.measureText(text).width;
};

const drawHexWithBuilding = (ctx: CanvasRenderingContext2D, id: TBuildingId, center: TPoint, size: number) => {
  traceHex(ctx, center, size);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  drawBuildingOnHex(ctx, id, center, size);
};

// ---------- Village section ----------

// The section of the village in the panel of the selected building (hex-modal.tsx, VillageSection).
const VILLAGE_SECTION_SIZE = { width: 380, height: 156 };

const drawVillageSection = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const { width } = VILLAGE_SECTION_SIZE;
  drawPanel(ctx, { x, y, ...VILLAGE_SECTION_SIZE });
  drawSectionHeader(ctx, "ДЕРЕВНЯ", x + 18, y + 14, x + width - 18);
  drawButton(ctx, { x: x + 18, y: y + 44, width: width - 36, height: 42 }, "▶ Запустить", { size: 16 });
  drawWrappedText(ctx, "Сама кубик не бросает. Без запуска не даёт жителей", x + 18, y + 100, width - 36, {
    size: 14,
    lineHeight: 19,
    color: PALETTE.muted,
  });
};

// ---------- Population section ----------

// The section of a building where people live: how many live there and how many it holds.
// A bar of places under the line: a filled place is a resident.
// The drawing follows the section "НАСЕЛЕНИЕ" of the Ostrov 3 document (ostrov-buildings).
const RESIDENTS_SECTION_SIZE = { width: 380, height: 100 };
const RESIDENTS = { lives: 3, holds: 5 };
const PLACE = { height: 12, gap: 6 };

const drawResidentsSection = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const { width } = RESIDENTS_SECTION_SIZE;
  drawPanel(ctx, { x, y, ...RESIDENTS_SECTION_SIZE });
  drawSectionHeader(ctx, "НАСЕЛЕНИЕ", x + 18, y + 14, x + width - 18);
  const lineY = y + 52;
  drawIcon(ctx, "population", x + 18, lineY - 12, 24);
  drawTextCenteredAt(ctx, `живёт ${RESIDENTS.lives} / вмещает ${RESIDENTS.holds}`, x + 50, lineY, { size: 18, weight: 500 });
  const placesWidth = width - 36;
  const placeWidth = (placesWidth - PLACE.gap * (RESIDENTS.holds - 1)) / RESIDENTS.holds;
  for (let index = 0; index < RESIDENTS.holds; index++) {
    const place: TRect = { x: x + 18 + index * (placeWidth + PLACE.gap), y: lineY + 22, width: placeWidth, height: PLACE.height };
    drawPanel(ctx, place, {
      fill: index < RESIDENTS.lives ? PALETTE.muted : PALETTE.paper,
      radius: 3,
      width: 1,
    });
  }
};

// ---------- Army section ----------

// The section of the military building (hex-modal.tsx, ArmySection).
// The recruit button is disabled: no idle people are left.
const ARMY_SECTION_SIZE = { width: 320, height: 166 };

const drawArmySection = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const { width } = ARMY_SECTION_SIZE;
  drawPanel(ctx, { x, y, ...ARMY_SECTION_SIZE });
  drawSectionHeader(ctx, "АРМИЯ", x + 18, y + 14, x + width - 18);
  const statusY = y + 58;
  drawIcon(ctx, "militia", x + 18, statusY - 14, 28);
  drawTextCenteredAt(ctx, "3", x + 52, statusY, { size: 20, weight: 700, color: PALETTE.positive });
  drawTextCenteredAt(ctx, "· свободны: 0", x + 70, statusY, { size: 17, color: PALETTE.muted });
  drawButton(ctx, { x: x + 18, y: y + 84, width: 140, height: 36 }, "− Распустить");
  drawButton(ctx, { x: x + 168, y: y + 84, width: 126, height: 36 }, "+ Призвать", { disabled: true });
  drawText(ctx, "Вся армия идёт в бой в фазе зачистки", x + 18, y + 134, { size: 14, color: PALETTE.muted });
};

// ---------- Population tooltip ----------

// The hint of the population cell in the resources block (resources-panel.tsx), over a part of the block.
const TOOLTIP = { width: 330, height: 150 };
const TOOLTIP_CELLS: THudState = {
  amounts: { food: 0, wood: 0, stone: 0, population: 0, mad: 0, hammers: 3, power: 0, scouting: 0, science: 0, mana: 0 },
  bright: ["population"],
};
const TOOLTIP_CELL = { width: 88, height: 40 };
const POPULATION_TOOLTIP_SIZE = { width: TOOLTIP.width, height: TOOLTIP.height + 18 + TOOLTIP_CELL.height + 16 };

const drawPopulationTooltip = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const rowY = y + TOOLTIP.height + 18;
  // The part of the block under the hint: population, mad people, hammers.
  drawPanel(ctx, { x, y: rowY, width: TOOLTIP_CELL.width * 3 + 16, height: TOOLTIP_CELL.height + 16 });
  (["population", "mad", "hammers"] as const).forEach((id, index) => {
    drawResourceCell(ctx, { x: x + 8 + index * TOOLTIP_CELL.width, y: rowY + 8, ...TOOLTIP_CELL }, id, TOOLTIP_CELLS);
  });

  drawPanel(ctx, { x, y, ...TOOLTIP }, { fill: PALETTE.paper, width: 2 });
  // The tail of the hint points at the population cell.
  const tailX = x + 40;
  ctx.beginPath();
  ctx.moveTo(tailX - 10, y + TOOLTIP.height);
  ctx.lineTo(tailX, y + TOOLTIP.height + 12);
  ctx.lineTo(tailX + 10, y + TOOLTIP.height);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 2;
  ctx.stroke();

  drawText(ctx, "Население", x + 16, y + 12, { size: 19, weight: 700 });
  drawText(ctx, "Работают в зданиях и служат в армии", x + 16, y + 42, { size: 14, color: PALETTE.muted });
  ["Свободно: 0", "Работают: 3", "Служат: 3"].forEach((row, index) => {
    drawText(ctx, row, x + 16, y + 66 + index * 24, { size: 15 });
  });
};

// ---------- Farm card ----------

// The farm with its popup (hex-tooltip.tsx) and the plate under the hex (hex-plates.tsx).
// The popup lists the faces of the die: food and toxicity. The dashed face is the rolled one.
const FARM_POPUP = { width: 380, height: 214 };
const FARM_HEX_SIZE = 58;
const FARM_CARD_SIZE = { width: FARM_POPUP.width, height: FARM_POPUP.height + 24 + FARM_HEX_SIZE * 2 + 18 };
const FARM_FACES: { food: number; toxicity: number }[] = [
  { food: 1, toxicity: 5 },
  { food: 5, toxicity: 3 },
  { food: 3, toxicity: 0 },
  { food: 3, toxicity: 3 },
];
const ROLLED_FACE = { food: 4, toxicity: 1 };

const drawFaceChip = (ctx: CanvasRenderingContext2D, rect: TRect, face: { food: number; toxicity: number }, dashed: boolean) => {
  drawPanel(ctx, rect, { fill: PALETTE.paper, radius: 6, width: 1, dashed });
  const middle = rect.y + rect.height / 2;
  const center = rect.x + rect.width / 2;
  drawIcon(ctx, "food", center - 50, middle - 10, 20);
  drawTextCenteredAt(ctx, String(face.food), center - 26, middle, { size: 15, weight: 600 });
  drawIcon(ctx, "toxicity", center + 4, middle - 10, 20);
  drawTextCenteredAt(ctx, String(face.toxicity), center + 28, middle, { size: 15, weight: 600 });
};

const drawFarmCard = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  drawPanel(ctx, { x, y, ...FARM_POPUP });
  drawImageContain(ctx, BUILDING_ART.farm, { x: x + 12, y: y + 8, width: 36, height: 36 });
  drawTextCenteredAt(ctx, "Ферма", x + 56, y + 27, { size: 19, weight: 700 });
  drawTextCenteredAt(ctx, "Умеренные луга", x + 56 + textWidth(ctx, "Ферма", 19, 700) + 12, y + 27, {
    size: 15,
    color: PALETTE.muted,
  });

  const chipWidth = (FARM_POPUP.width - 14 * 3) / 2;
  FARM_FACES.forEach((face, index) => {
    const column = index % 2;
    const row = Math.floor(index / 2);
    drawFaceChip(
      ctx,
      { x: x + 14 + column * (chipWidth + 14), y: y + 56 + row * 40, width: chipWidth, height: 32 },
      face,
      false,
    );
  });
  drawFaceChip(ctx, { x: x + FARM_POPUP.width / 2 - 65, y: y + 136, width: 130, height: 32 }, ROLLED_FACE, true);

  // The workers row: the point of this card.
  const workersRow: TRect = { x: x + 8, y: y + 176, width: FARM_POPUP.width - 16, height: 30 };
  ctx.fillStyle = PALETTE.highlightFill;
  ctx.fillRect(workersRow.x, workersRow.y, workersRow.width, workersRow.height);
  drawIcon(ctx, "population", x + 14, workersRow.y + 3, 24);
  drawTextCenteredAt(ctx, "Рабочие: 1/1", x + 46, workersRow.y + 15, { size: 15, weight: 700 });

  const hexCenter = { x: x + FARM_POPUP.width / 2, y: y + FARM_POPUP.height + 24 + FARM_HEX_SIZE };
  drawHexWithBuilding(ctx, "farm", hexCenter, FARM_HEX_SIZE);

  // The plate under the building: workers present of needed, and the growth of the hex toxicity.
  const plate: TRect = { x: hexCenter.x - 110, y: hexCenter.y + FARM_HEX_SIZE - 26, width: 220, height: CARD_PLATE.height };
  drawWorkersPlate(ctx, plate, CARD_PLATE);
};

// ---------- Barracks card ----------

// The military building on its hex, with its panel section to the right.
const BARRACKS_HEX_SIZE = 56;
const BARRACKS_GAP = 20;
const BARRACKS_CARD_SIZE = {
  width: Math.sqrt(3) * BARRACKS_HEX_SIZE + BARRACKS_GAP + ARMY_SECTION_SIZE.width,
  height: Math.max(BARRACKS_HEX_SIZE * 2, ARMY_SECTION_SIZE.height),
};

const drawBarracksCard = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const hexHalfWidth = (Math.sqrt(3) / 2) * BARRACKS_HEX_SIZE;
  const middle = topLeft.y + BARRACKS_CARD_SIZE.height / 2;
  drawHexWithBuilding(ctx, "barracks", { x: topLeft.x + hexHalfWidth, y: middle }, BARRACKS_HEX_SIZE);
  drawArmySection(ctx, {
    x: topLeft.x + hexHalfWidth * 2 + BARRACKS_GAP,
    y: middle - ARMY_SECTION_SIZE.height / 2,
  });
};

// ---------- Slot modal ----------

// The toxicity slot when the collection phase ends (slot-modal.tsx).
// The event is "Испарения" from the catalog of ostrov-prototype (src/core/toxic-slot.ts):
// level "Малые", three bad symbols, two people go mad.
const SLOT_MODAL_SIZE = { width: 400, height: 450 };
const REEL_SIZE = 96;
const REEL_GAP = 16;
const SLOT_REELS: TIconId[] = ["dead", "poison", "dead"];
const SLOT_EVENT_TEXT = "Над стоками поднялся сладковатый дым. Двое жителей перестали узнавать своих.";
const SLOT_MAD_LINE = "2 жителя сошли с ума";

// The madness line inside the event block.
const slotMadLine = (topLeft: TPoint): TRect => ({
  x: topLeft.x + 28,
  y: topLeft.y + 326,
  width: SLOT_MODAL_SIZE.width - 56,
  height: 30,
});

const drawSlotModal = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const { width } = SLOT_MODAL_SIZE;
  drawPanel(ctx, { x, y, ...SLOT_MODAL_SIZE }, { fill: PALETTE.paper, width: 2.5, radius: 10 });
  drawText(ctx, "Слот заражения", x + 20, y + 18, { size: 20, weight: 700 });
  drawText(ctx, "Малые последствия", x + width - 20, y + 22, { size: 13, color: PALETTE.muted, align: "right" });

  const reelsLeft = x + (width - (REEL_SIZE * 3 + REEL_GAP * 2)) / 2;
  SLOT_REELS.forEach((icon, index) => {
    const reel: TRect = { x: reelsLeft + index * (REEL_SIZE + REEL_GAP), y: y + 60, width: REEL_SIZE, height: REEL_SIZE };
    drawPanel(ctx, reel, { fill: PALETTE.negativeFill, color: PALETTE.negative, width: 2, radius: 8 });
    drawIcon(ctx, icon, reel.x + 18, reel.y + 18, REEL_SIZE - 36);
  });

  const event: TRect = { x: x + 20, y: y + 172, width: width - 40, height: 194 };
  drawPanel(ctx, event, { fill: PALETTE.paper, color: PALETTE.negative, width: 1.5 });
  drawIcon(ctx, "mad", event.x + 10, event.y + 10, 46);
  drawText(ctx, "Бедствие", event.x + 66, event.y + 12, { size: 13, weight: 700, color: PALETTE.negative });
  drawText(ctx, "Испарения", event.x + 66, event.y + 30, { size: 19, weight: 700 });
  drawWrappedText(ctx, SLOT_EVENT_TEXT, event.x + 12, event.y + 68, event.width - 24, { size: 14, lineHeight: 20 });

  // The line of the madness: healthy people become mad people for good.
  const line = slotMadLine(topLeft);
  ctx.fillStyle = PALETTE.negativeFill;
  ctx.fillRect(line.x, line.y, line.width, line.height);
  drawIcon(ctx, "mad", line.x + 6, line.y + 3, 24);
  drawTextInRect(ctx, SLOT_MAD_LINE, { ...line, x: line.x + 36, width: textWidth(ctx, SLOT_MAD_LINE, 15, 600) }, {
    size: 15,
    weight: 600,
    color: PALETTE.negative,
  });

  drawButton(ctx, { x: x + width / 2 - 130, y: y + 390, width: 260, height: 40 }, "Перейти к фазе разведки");
};

// ---------- Becoming mad ----------

// A person becomes a mad person: the population icon, an arrow and the mad icon.
const BECOME_MAD_SIZE = { width: 220, height: 72 };

const drawBecomeMad = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const icon = BECOME_MAD_SIZE.height;
  drawIcon(ctx, "population", x, y, icon);
  const middle = y + icon / 2;
  ctx.beginPath();
  ctx.moveTo(x + icon + 14, middle);
  ctx.lineTo(x + BECOME_MAD_SIZE.width - icon - 14, middle);
  ctx.moveTo(x + BECOME_MAD_SIZE.width - icon - 26, middle - 8);
  ctx.lineTo(x + BECOME_MAD_SIZE.width - icon - 14, middle);
  ctx.lineTo(x + BECOME_MAD_SIZE.width - icon - 26, middle + 8);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 2;
  ctx.stroke();
  drawIcon(ctx, "mad", x + BECOME_MAD_SIZE.width - icon, y, icon);
};

export {
  BARRACKS_CARD_SIZE,
  BECOME_MAD_SIZE,
  FARM_CARD_SIZE,
  RESIDENTS_SECTION_SIZE,
  POPULATION_TOOLTIP_SIZE,
  SLOT_MODAL_SIZE,
  VILLAGE_SECTION_SIZE,
  drawBarracksCard,
  drawBecomeMad,
  drawFarmCard,
  drawResidentsSection,
  drawPopulationTooltip,
  drawSlotModal,
  drawVillageSection,
  slotMadLine,
};
