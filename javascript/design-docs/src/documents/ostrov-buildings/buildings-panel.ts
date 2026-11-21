import { drawImageContain } from "../../kit/image";
import { drawText } from "../../kit/shapes";
import { BUILDING_ART } from "./art";
import { bottomPanel } from "./screen";
import { STYLE, centerXOf, rect } from "./wire";
import type { TBuildingArt } from "./art";

// The buildings panel of ostrov-prototype (src/ui/components/buildings-panel.tsx):
// the bottom bar with one card per building. A click on a card arms the building,
// the next click on a hex of the island places it.
// The cards of the prototype show only the art and give the name on hover.
// Here every card also has its name under the art, so the wireframe reads without hover.

const CARDS: { art: TBuildingArt; name: string }[] = [
  { art: "farm", name: "Ферма" },
  { art: "mine", name: "Рудник" },
  { art: "sawmill", name: "Лесопилка" },
  { art: "village", name: "Деревня" },
  { art: "masonsGuild", name: "Гильдия\nмасонов" },
  { art: "observatory", name: "Обсерватория" },
  { art: "university", name: "Университет" },
  { art: "converter", name: "Центральный\nконвертер" },
];

const PADDING = 8;
const CARD = { width: 86, gap: 4 };
const ART_SIDE = 56;
const NAME = { size: 10, lineHeight: 12 };

const BUILDINGS_PANEL = bottomPanel(495, CARDS.length * CARD.width + (CARDS.length - 1) * CARD.gap + PADDING * 2);

const drawBuildingsPanel = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, BUILDINGS_PANEL, 8, STYLE.panel);
  const top = BUILDINGS_PANEL.y + PADDING;
  const height = BUILDINGS_PANEL.height - PADDING * 2;
  CARDS.forEach((card, index) => {
    const area = { x: BUILDINGS_PANEL.x + PADDING + index * (CARD.width + CARD.gap), y: top, width: CARD.width, height };
    rect(ctx, area, 6, STYLE.chip);
    drawImageContain(ctx, BUILDING_ART[card.art], {
      x: centerXOf(area) - ART_SIDE / 2,
      y: area.y + 2,
      width: ART_SIDE,
      height: ART_SIDE,
    });
    // The name stands in the free strip under the art, centered on it.
    const lines = card.name.split("\n").length;
    const strip = { top: area.y + ART_SIDE + 2, bottom: area.y + area.height - 3 };
    const nameTop = (strip.top + strip.bottom) / 2 - (lines * NAME.lineHeight) / 2;
    drawText(ctx, card.name, centerXOf(area), nameTop, { ...NAME, align: "center" });
  });
};

export { BUILDINGS_PANEL, drawBuildingsPanel };
