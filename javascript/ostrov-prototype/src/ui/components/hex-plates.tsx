import { useSignals } from "@preact/signals-react/runtime";
import { hexDie } from "../../core/dice";
import { HEX_SIZE, hexToPixel } from "../../core/hex";
import { ICONS } from "../../core/icons";
import { isStaffed, workersNeeded, workersOn } from "../../core/population";
import { revealTiming } from "../../core/production-reveal";
import { getResource } from "../../core/resources";
import { POWER_PER_TURN } from "../../core/stronghold";
import { structureHp } from "../../core/structure-hp";
import { facePayout, isDead, isFoodBlocked } from "../../core/tax";
import { findRoll } from "../../core/tax-plan";
import { useStore } from "../../store/store";
import type { CSSProperties, FC } from "react";
import type { THexDie } from "../../core/dice";
import type { TRevealTiming } from "../../core/production-reveal";
import type { TPayoutEffects } from "../../core/tax";
import type { TTaxRoll } from "../../core/tax-plan";
import type { THex, TPlayer } from "../../core/types";
import type { TProductionReveal } from "../../store/ui-state";

/**
 * Two kinds of plates on the island, both in the canvas's own units, so they
 * zoom with the island. Below `MIN_SCALE` the text is too small to read, and
 * the plates are hidden.
 *
 * - Under every die: the hex's toxicity meter. It sits on the lower edge of the
 *   hex, over the scorched base of the 256px sprite. A building's workers or
 *   the stronghold's army lead the plate. A thin hp bar runs along the bottom
 *   of the plate under a building or the stronghold.
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
/** The space between the workers and the toxicity meter on one plate. */
const STAFF_GAP = 4;
/** The hp bar at the bottom of the plate, and the strip it adds to the plate. */
const HP_BAR = 1.6;
const HP_ROW = 2.5;
/** At or below this share of max hp the bar turns red. */
const HP_CRITICAL_SHARE = 0.35;
/** The space between the toxicity percent and the coming "+N%". */
const GAIN_GAP = 2.5;

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

/** The people block at the start of a plate: an icon and a count. */
type TStaffBlock = {
  readonly icon: string;
  readonly text: string;
  /** Red text and a dim icon: the building lacks workers. */
  readonly isShort: boolean;
};

/**
 * The people block of the hex. A building shows its workers: the person icon
 * and "1/1". The stronghold shows its army: the militia icon and the soldier
 * count. The army has no cap, so the count stands alone.
 */
const staffBlock = (hex: THex, die: THexDie, army: number): TStaffBlock | null => {
  if (die.source === "stronghold") {
    return { icon: ICONS.militia, text: `${army}`, isShort: false };
  }

  if (hex.building) {
    return { icon: ICONS.population, text: `${workersOn(hex)}/${workersNeeded(hex)}`, isShort: !isStaffed(hex) };
  }

  return null;
};

type THpLevel = "ok" | "hurt" | "critical" | "ruined";

/** The hp of the structure on the hex as a bar share and a colour level. */
const hpBar = (player: TPlayer, hex: THex): { share: number; level: THpLevel } | null => {
  const state = structureHp(player, hex);
  if (!state) {
    return null;
  }

  const share = Math.max(0, Math.min(1, state.hp / state.max));
  let level: THpLevel = "ok";
  if (state.hp <= 0) {
    level = "ruined";
  } else if (share <= HP_CRITICAL_SHARE) {
    level = "critical";
  } else if (share < 1) {
    level = "hurt";
  }

  return { share, level };
};

type TToxicityLevel = "clean" | "some" | "high" | "dead";

/** The colour of a toxicity value: red on a dead hex, orange where food stops. */
const toxicityLevel = (hex: THex): TToxicityLevel => {
  if (isDead(hex)) {
    return "dead";
  }

  if (isFoodBlocked(hex)) {
    return "high";
  }

  return Math.round(hex.toxicity) > 0 ? "some" : "clean";
};

/**
 * The toxicity the chosen face will leave on the hex, in percent. It counts
 * only while the dice lie unpaid: the collection then adds it for real.
 */
const pendingToxicity = (hex: THex, roll: TTaxRoll | null, effects: TPayoutEffects) => {
  const face = roll ? roll.faces[roll.chosenIndex] : undefined;

  return face ? facePayout(face, hex, effects).toxicity : 0;
};

type TToxicityPlateProps = {
  hex: THex;
  staff: TStaffBlock | null;
  hp: { share: number; level: THpLevel } | null;
  /** The toxicity the tax phase is about to add, in percent. 0 shows nothing. */
  gain: number;
};

/**
 * The plate under a die. It starts with the people block (see `staffBlock`).
 * Then comes the toxicity meter: the flask, the bar and the percent. A
 * structure adds its hp bar along the bottom edge: green at full hp, orange
 * when damaged, red when critical. Ruins get an empty red bar and a red frame.
 * In the tax phase a pulsing segment on the bar and a "+N%" label show the
 * toxicity the chosen face will add. The segment turns orange or red when
 * the hex will cross a threshold.
 */
const ToxicityPlate: FC<TToxicityPlateProps> = ({ hex, staff, hp, gain }) => {
  const center = hexToPixel(hex.q, hex.r, HEX_SIZE);
  const toxicity = Math.round(hex.toxicity);
  const meterText = `${toxicity}%`;
  const staffText = staff?.text ?? null;
  const staffWidth = staffText === null ? 0 : METER_ICON + GAP + staffText.length * CHAR_WIDTH + STAFF_GAP;
  const gainText = gain > 0 ? `+${gain}%` : null;
  const gainWidth = gainText === null ? 0 : GAIN_GAP + gainText.length * CHAR_WIDTH;
  const meterWidth =
    staffWidth + METER_ICON + GAP + METER_WIDTH + GAP + meterText.length * CHAR_WIDTH + gainWidth;
  const width = meterWidth + PAD_X * 2;
  const plateHeight = hp === null ? METER_HEIGHT : METER_HEIGHT + HP_ROW;
  // The hp strip pushes the meter row up a little, so both fit the plate.
  const meterY = ROW_TOP + (hp === null ? METER_HEIGHT / 2 : METER_HEIGHT / 2 - 0.5);
  const hpY = ROW_TOP + plateHeight - HP_BAR - 1.6;
  const staffLeft = -meterWidth / 2;
  const meterLeft = staffLeft + staffWidth;
  const barLeft = meterLeft + METER_ICON + GAP;
  const level = toxicityLevel(hex);
  const filled = (METER_WIDTH * Math.min(100, toxicity)) / 100;
  const pendingWidth = (METER_WIDTH * Math.min(100, toxicity + gain)) / 100 - filled;
  const pendingLevel = toxicityLevel({ ...hex, toxicity: hex.toxicity + gain });

  return (
    <g
      className={`hex-plate ${hp?.level === "ruined" ? "hex-plate--ruined" : ""}`}
      transform={`translate(${center.x} ${center.y})`}
    >
      <rect className="hex-plate__back" x={-width / 2} y={ROW_TOP} width={width} height={plateHeight} rx={4} />

      {staff === null ? null : (
        <g className={`hex-plate__staff ${staff.isShort ? "hex-plate__staff--short" : ""}`}>
          <image href={staff.icon} x={staffLeft} y={meterY - METER_ICON / 2} width={METER_ICON} height={METER_ICON} />

          <text
            className="hex-plate__text hex-plate__text--staff"
            x={staffLeft + METER_ICON + GAP}
            y={meterY}
            fontSize={FONT * 0.85}
            dominantBaseline="central"
          >
            {staffText}
          </text>
        </g>
      )}

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
          width={filled}
          height={METER_BAR}
          rx={METER_BAR / 2}
        />
      ) : null}

      {gainText !== null && pendingWidth > 0 ? (
        <rect
          className={`hex-plate__meter-pending hex-plate__meter-pending--${pendingLevel}`}
          x={barLeft + filled}
          y={meterY - METER_BAR / 2}
          width={pendingWidth}
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

        {/* A tspan follows the real width of the percent, so the two never overlap. */}
        {gainText === null ? null : (
          <tspan className={`hex-plate__gain hex-plate__gain--${pendingLevel}`} dx={GAIN_GAP}>
            {gainText}
          </tspan>
        )}
      </text>

      {hp === null ? null : (
        <g className={`hex-plate__hp hex-plate__hp--${hp.level}`}>
          <rect
            className="hex-plate__hp-track"
            x={-meterWidth / 2}
            y={hpY}
            width={meterWidth}
            height={HP_BAR}
            rx={HP_BAR / 2}
          />

          {hp.share > 0 ? (
            <rect
              className="hex-plate__hp-fill"
              x={-meterWidth / 2}
              y={hpY}
              width={meterWidth * hp.share}
              height={HP_BAR}
              rx={HP_BAR / 2}
            />
          ) : null}
        </g>
      )}
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
  // The coming toxicity stands until the collection starts to add it to the hexes.
  const showsPending = !isReadonly && taxStatus === "rolled";
  const dice = player.island.hexes.flatMap((hex) => {
    const die = hexDie(player, hex);

    return die ? [{ hex, die }] : [];
  });
  // Every structure gets a plate. Ruins of the stronghold roll no die, but keep the plate with the empty hp bar.
  const plated = player.island.hexes.filter((hex) => structureHp(player, hex) !== null);

  return (
    <g className="hex-plates">
      {plated.map((hex) => {
        const die = hexDie(player, hex);

        return (
          <ToxicityPlate
            key={hex.id}
            hex={hex}
            staff={die ? staffBlock(hex, die, player.army) : null}
            hp={hpBar(player, hex)}
            gain={showsPending && die ? pendingToxicity(hex, findRoll(taxPlan, hex.id), effects) : 0}
          />
        );
      })}

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
