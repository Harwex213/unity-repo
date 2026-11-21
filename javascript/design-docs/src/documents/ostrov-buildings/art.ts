import { createImageAsset } from "../../kit/image";
import converterArt from "./assets/converter.png";
import farmArt from "./assets/farm.png";
import demolishIcon from "./assets/icon-demolish.png";
import populationIcon from "./assets/icon-population.png";
import toxicityIcon from "./assets/icon-toxicity.png";
import masonsGuildArt from "./assets/masons-guild.png";
import mineArt from "./assets/mine.png";
import observatoryArt from "./assets/observatory.png";
import sawmillArt from "./assets/sawmill.png";
import strongholdArt from "./assets/stronghold.png";
import universityArt from "./assets/university.png";
import villageArt from "./assets/village.png";
import type { TImageAsset } from "../../kit/image";

// Every image of the document.
// The buildings come from ostrov-prototype (src/assets/hex-buildings): the 256px sprites of the island canvas.
// The icons come from ostrov-prototype (src/assets/icons): the 64px pictograms of the HUD.

const BUILDING_ART = {
  farm: createImageAsset(farmArt),
  mine: createImageAsset(mineArt),
  sawmill: createImageAsset(sawmillArt),
  village: createImageAsset(villageArt),
  masonsGuild: createImageAsset(masonsGuildArt),
  observatory: createImageAsset(observatoryArt),
  university: createImageAsset(universityArt),
  converter: createImageAsset(converterArt),
};
type TBuildingArt = keyof typeof BUILDING_ART;

// The stronghold is not one of the buildings of the panel: it cannot be built or demolished.
const STRONGHOLD_ART = createImageAsset(strongholdArt);

const ICONS = {
  population: createImageAsset(populationIcon),
  toxicity: createImageAsset(toxicityIcon),
  demolish: createImageAsset(demolishIcon),
};

const ART_ASSETS: TImageAsset[] = [...Object.values(BUILDING_ART), STRONGHOLD_ART, ...Object.values(ICONS)];

export { ART_ASSETS, BUILDING_ART, ICONS, STRONGHOLD_ART };
export type { TBuildingArt };
