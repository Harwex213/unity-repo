import { useSignals } from "@preact/signals-react/runtime";
import { hexDie } from "../../core/dice";
import { HEX_SIZE, hexToPixel } from "../../core/hex";
import { ICONS } from "../../core/icons";
import { revealTiming } from "../../core/production-reveal";
import { getResource } from "../../core/resources";
import { POWER_PER_TURN } from "../../core/stronghold";
import { facePayout, isDead, isFoodBlocked } from "../../core/tax";
import { findRoll } from "../../core/tax-plan";
import { useStore } from "../../store/store";
import type { CSSProperties, FC } from "react";
import type { THexDie } from "../../core/dice";
import type { TRevealTiming } from "../../core/production-reveal";
import type { TPayoutEffects } from "../../core/tax";
import type { TTaxRoll } from "../../core/tax-plan";
import type { THex } from "../../core/types";
import type { TProductionReveal } from "../../store/ui-state";

/**
 * Two kinds of plates on the island, both in the canvas's own units, so they
 * zoom with the island. Below `MIN_SCALE` the text is too small to read, and
 * the plates are hidden.
 *
 * - Under every die: the hex's toxicity meter. It sits on the lower edge of the
 *   hex, over the scorched base of the 256px sprite.
 * - Above every building in the tax phase: the rolled payout, one small plate
 *   per resource. The plates pop up after the building's production pulse
 *   (see `core/production-reveal.ts`) and fade out as the payout flies away.
 */

const MIN_SCALE = 0.6;
const FONT = 9;
const ICON = 11;
const GAP = 2;
const PAD_X = 4;
/** Estimated advance of one glyph of the plate font, in canvas units. */
const CHAR_WIDTH = FONT * 0.6;
const ROW_TOP = HEX_SIZE * 0.4;
const METER_HEIGHT = 12;
const METER_WIDTH = 28;
const METER_BAR = 3.5;
const METER_ICON = ICON * 0.8;

/** The production plates: the lowest one sits on the roof line of the sprite. */
const PROD_BOTTOM = -HEX_SIZE * 0.58;
const PROD_HEIGHT = 13;
const PROD_GAP = 2;
const PROD_PAD_X = 3.5;
const PROD_FONT = 9.5;

type TProdTone = "yield" | "toxic" | "power" | "dim";

type TProdPlate = {
  readonly key: string;
  readonly icon: string;
  readonly text: string;
  readonly tone: TProdTone;
  /** The face was picked for power, not rolled: the plate says so with a crown. */
  readonly isPicked: boolean;
};

/** What the chosen face will really pay, one plate per resource. */
const productionPlates = (die: THexDie, hex: THex, roll: TTaxRoll, effects: TPayoutEffects): TProdPlate[] => {
  const face = roll.faces[roll.chosenIndex];
  if (!face) {
    return [];
  }

  const paid = facePayout(face, hex, effects);
  const resource = getResource(face.resource);
  const plates: TProdPlate[] = [
    {
      key: "yield",
      icon: resource.icon,
      text: `+${paid.amount}`,
      tone: paid.amount > 0 ? "yield" : "dim",
      isPicked: roll.chosenIndex !== roll.rolledIndex,
    },
  ];

  if (die.source === "stronghold") {
    plates.push({ key: "power", icon: ICONS.power, text: `+${POWER_PER_TURN}`, tone: "power", isPicked: false });
  } else if (paid.toxicity > 0) {
    plates.push({ key: "toxicity", icon: ICONS.toxicity, text: `+${paid.toxicity}%`, tone: "toxic", isPicked: false });
  }

  return plates;
};

type TProdPlateProps = {
  plate: TProdPlate;
  /** The plate's lower edge, in the hex's units. */
  bottom: number;
  /** When it pops, or `null` when it stands still. */
  popDelayMs: number | null;
};

const ProdPlate: FC<TProdPlateProps> = ({ plate, bottom, popDelayMs }) => {
  const pickedWidth = plate.isPicked ? ICON * 0.8 + GAP : 0;
  const width = PROD_PAD_X * 2 + pickedWidth + ICON + GAP + plate.text.length * PROD_FONT * 0.66;
  const left = -width / 2;
  const top = bottom - PROD_HEIGHT;
  const centerY = top + PROD_HEIGHT / 2;
  const style: CSSProperties | undefined = popDelayMs === null ? undefined : { animationDelay: `${popDelayMs}ms` };

  return (
    <g
      className={`prod-plate ${popDelayMs === null ? "" : "prod-plate--pop"} ${plate.isPicked ? "prod-plate--picked" : ""}`}
      style={style}
    >
      <rect className="prod-plate__back" x={left} y={top} width={width} height={PROD_HEIGHT} rx={3.5} />

      {plate.isPicked ? (
        <image
          href={ICONS.power}
          x={left + PROD_PAD_X}
          y={centerY - (ICON * 0.8) / 2}
          width={ICON * 0.8}
          height={ICON * 0.8}
        />
      ) : null}

      <image
        href={plate.icon}
        x={left + PROD_PAD_X + pickedWidth}
        y={centerY - ICON / 2}
        width={ICON}
        height={ICON}
      />

      <text
        className={`prod-plate__text prod-plate__text--${plate.tone}`}
        x={left + PROD_PAD_X + pickedWidth + ICON + GAP}
        y={centerY}
        fontSize={PROD_FONT}
        dominantBaseline="central"
      >
        {plate.text}
      </text>
    </g>
  );
};

type TProdStackProps = {
  hex: THex;
  plates: readonly TProdPlate[];
  reveal: TProductionReveal | null;
  timing: TRevealTiming;
};

/** The plates of one building, stacked upwards in the order they pop. */
const ProdStack: FC<TProdStackProps> = ({ hex, plates, reveal, timing }) => {
  const center = hexToPixel(hex.q, hex.r, HEX_SIZE);
  const start = reveal && !reveal.done ? reveal.delays[hex.id] : undefined;
  const leaveMs = reveal?.leaving ? (reveal.leaving[hex.id] ?? 0) : null;
  const style: CSSProperties | undefined = leaveMs === null ? undefined : { animationDelay: `${leaveMs}ms` };

  return (
    <g
      className={`prod-plates ${leaveMs === null ? "" : "prod-plates--leaving"}`}
      transform={`translate(${center.x} ${center.y})`}
      style={style}
    >
      {plates.map((plate, index) => (
        <ProdPlate
          key={plate.key}
          plate={plate}
          bottom={PROD_BOTTOM - index * (PROD_HEIGHT + PROD_GAP)}
          popDelayMs={start === undefined ? null : start + timing.plateAtMs + index * timing.plateStepMs}
        />
      ))}
    </g>
  );
};

type TToxicityPlateProps = {
  hex: THex;
};

/** The toxicity meter under a die: the flask, the bar and the percent. */
const ToxicityPlate: FC<TToxicityPlateProps> = ({ hex }) => {
  const center = hexToPixel(hex.q, hex.r, HEX_SIZE);
  const toxicity = Math.round(hex.toxicity);
  const meterText = `${toxicity}%`;
  const meterWidth = METER_ICON + GAP + METER_WIDTH + GAP + meterText.length * CHAR_WIDTH;
  const width = meterWidth + PAD_X * 2;
  const meterY = ROW_TOP + METER_HEIGHT / 2;
  const meterLeft = -meterWidth / 2;
  const barLeft = meterLeft + METER_ICON + GAP;
  const level = isDead(hex) ? "dead" : isFoodBlocked(hex) ? "high" : toxicity > 0 ? "some" : "clean";

  return (
    <g className="hex-plate" transform={`translate(${center.x} ${center.y})`}>
      <rect className="hex-plate__back" x={-width / 2} y={ROW_TOP} width={width} height={METER_HEIGHT} rx={4} />

      <image href={ICONS.toxicity} x={meterLeft} y={meterY - METER_ICON / 2} width={METER_ICON} height={METER_ICON} />

      <rect
        className="hex-plate__meter"
        x={barLeft}
        y={meterY - METER_BAR / 2}
        width={METER_WIDTH}
        height={METER_BAR}
        rx={METER_BAR / 2}
      />

      {toxicity > 0 ? (
        <rect
          className={`hex-plate__meter-fill hex-plate__meter-fill--${level}`}
          x={barLeft}
          y={meterY - METER_BAR / 2}
          width={(METER_WIDTH * Math.min(100, toxicity)) / 100}
          height={METER_BAR}
          rx={METER_BAR / 2}
        />
      ) : null}

      <text
        className={`hex-plate__text hex-plate__text--meter hex-plate__text--${level}`}
        x={barLeft + METER_WIDTH + GAP}
        y={meterY}
        fontSize={FONT * 0.85}
        dominantBaseline="central"
      >
        {meterText}
      </text>
    </g>
  );
};

type THexPlatesProps = {
  scale: number;
};

/**
 * Every plate of the viewed island, drawn above the hexes and their outlines.
 * The production plates go after every toxicity plate, so a plate above a
 * building is never covered by the meter of the hex above it.
 */
const HexPlates: FC<THexPlatesProps> = ({ scale }) => {
  useSignals();
  const store = useStore();
  const player = store.derived.viewedPlayer.value;
  const isReadonly = store.derived.isReadonly.value;
  const taxPlan = store.derived.humanTaxPlan.value;
  const taxStatus = store.game.tax.value?.status ?? null;
  const reveal = store.ui.productionReveal.value;
  const effects = store.derived.techEffects.value;
  const stage = store.game.stage.value;

  if (!player || scale < MIN_SCALE || stage === "setup" || stage === "starting") {
    return null;
  }

  const timing = revealTiming();
  // The payout plates stand while the dice lie unpaid and while they fly away.
  const showsProduction = !isReadonly && (taxStatus === "rolled" || taxStatus === "collecting");
  const dice = player.island.hexes.flatMap((hex) => {
    const die = hexDie(player, hex);

    return die ? [{ hex, die }] : [];
  });

  return (
    <g className="hex-plates">
      {dice.map(({ hex }) => (
        <ToxicityPlate key={hex.id} hex={hex} />
      ))}

      {showsProduction
        ? dice.map(({ hex, die }) => {
            const roll = findRoll(taxPlan, hex.id);
            if (!roll) {
              return null;
            }

            return (
              <ProdStack
                key={`prod-${hex.id}`}
                hex={hex}
                plates={productionPlates(die, hex, roll, effects)}
                reveal={reveal}
                timing={timing}
              />
            );
          })
        : null}
    </g>
  );
};

export { HexPlates };
