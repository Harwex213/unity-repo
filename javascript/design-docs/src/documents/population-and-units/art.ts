import { createImageAsset } from "../../kit/image";
import barracksArt from "./assets/barracks.png";
import farmArt from "./assets/building-farm.png";
import masonsGuildArt from "./assets/building-masons-guild.png";
import mineArt from "./assets/building-mine.png";
import observatoryArt from "./assets/building-observatory.png";
import sawmillArt from "./assets/building-sawmill.png";
import universityArt from "./assets/building-university.png";
import villageArt from "./assets/building-village.png";
import deadIcon from "./assets/icon-dead.png";
import foodIcon from "./assets/icon-food.png";
import hammersIcon from "./assets/icon-hammers.png";
import manaIcon from "./assets/icon-mana.png";
import militiaIcon from "./assets/icon-militia.png";
import poisonIcon from "./assets/icon-poison.png";
import powerIcon from "./assets/icon-power.png";
import scienceIcon from "./assets/icon-science.png";
import scoutingIcon from "./assets/icon-scouting.png";
import stoneIcon from "./assets/icon-stone.png";
import toxicityIcon from "./assets/icon-toxicity.png";
import woodIcon from "./assets/icon-wood.png";
import madIcon from "./assets/mad.png";
import madPersonArt from "./assets/mad-person.png";
import populationIcon from "./assets/population.png";
import type { TImageAsset } from "../../kit/image";

// Every image of the document.
// The buildings come from ostrov-prototype (src/assets/hex-buildings): the 256px sprites of the island canvas.
// The barracks was made for this document in the same style. The prototype has no barracks yet.
// The icons come from ostrov-prototype (src/assets/icons): the 64px pictograms of the HUD.
// The mad person picture comes from the user.

const BUILDING_ART = {
  barracks: createImageAsset(barracksArt),
  farm: createImageAsset(farmArt),
  masonsGuild: createImageAsset(masonsGuildArt),
  mine: createImageAsset(mineArt),
  observatory: createImageAsset(observatoryArt),
  sawmill: createImageAsset(sawmillArt),
  university: createImageAsset(universityArt),
  village: createImageAsset(villageArt),
};
type TBuildingId = keyof typeof BUILDING_ART;

const ICON_ART = {
  dead: createImageAsset(deadIcon),
  food: createImageAsset(foodIcon),
  hammers: createImageAsset(hammersIcon),
  mad: createImageAsset(madIcon),
  mana: createImageAsset(manaIcon),
  militia: createImageAsset(militiaIcon),
  poison: createImageAsset(poisonIcon),
  population: createImageAsset(populationIcon),
  power: createImageAsset(powerIcon),
  science: createImageAsset(scienceIcon),
  scouting: createImageAsset(scoutingIcon),
  stone: createImageAsset(stoneIcon),
  toxicity: createImageAsset(toxicityIcon),
  wood: createImageAsset(woodIcon),
};
type TIconId = keyof typeof ICON_ART;

const MAD_PERSON_ART = createImageAsset(madPersonArt);

const ART_ASSETS: TImageAsset[] = [...Object.values(BUILDING_ART), ...Object.values(ICON_ART), MAD_PERSON_ART];

export { ART_ASSETS, BUILDING_ART, ICON_ART, MAD_PERSON_ART };
export type { TBuildingId, TIconId };
