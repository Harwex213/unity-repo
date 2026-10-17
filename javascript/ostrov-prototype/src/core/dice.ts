import { facesOn, getBuilding } from "./buildings";
import {
  isStrongholdHex,
  STRONGHOLD_ALWAYS_YIELDS,
  STRONGHOLD_ART,
  STRONGHOLD_FACES,
  STRONGHOLD_LABEL,
} from "./stronghold";
import { isRuinedStronghold } from "./structure-hp";
import type { TAlwaysYield, TFace, THex, TPlayer } from "./types";

/**
 * The die that stands on a hex: a building's die on its biome, or the
 * stronghold's die. The tax phase rolls it, the pick popup lists it, and the
 * plate on the hex averages it. Everything that asks "what does this hex roll"
 * asks here.
 */

type THexDieSource = "building" | "stronghold";

type THexDie = {
  readonly source: THexDieSource;
  readonly label: string;
  /** The 64px icon of what stands on the hex. */
  readonly art: string;
  readonly faces: readonly TFace[];
  /** Index of the face the biome adds, or -1 when the die has none. */
  readonly biomeFaceIndex: number;
  /** What the hex pays every tax phase on top of the roll. Empty for a building. */
  readonly alwaysYields: readonly TAlwaysYield[];
};

const hexDie = (player: TPlayer | null, hex: THex): THexDie | null => {
  if (isStrongholdHex(player, hex.id)) {
    // Ruins left by a lost battle roll nothing until they are repaired.
    if (isRuinedStronghold(player, hex)) {
      return null;
    }

    return {
      source: "stronghold",
      label: STRONGHOLD_LABEL,
      art: STRONGHOLD_ART,
      faces: STRONGHOLD_FACES,
      biomeFaceIndex: -1,
      alwaysYields: STRONGHOLD_ALWAYS_YIELDS,
    };
  }

  if (!hex.building) {
    return null;
  }

  const building = getBuilding(hex.building);
  const faces = facesOn(building, hex.biome);
  const hasBiomeFace = building.biomeFaces[hex.biome] !== undefined;

  return {
    source: "building",
    label: building.label,
    art: building.art,
    faces,
    biomeFaceIndex: hasBiomeFace ? faces.length - 1 : -1,
    alwaysYields: [],
  };
};

export type { THexDie, THexDieSource };
export { hexDie };
