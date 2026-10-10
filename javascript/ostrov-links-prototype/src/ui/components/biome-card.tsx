import { averageAmount, averageToxicity, facesOn } from "../../core/links";
import { formatAverage } from "../format";
import { FaceEditor } from "./face-editor";
import type { FC } from "react";
import type { TFacePatch } from "../../core/links";
import type { TBiome, TBuildingLinks } from "../../core/types";

type TBiomeCardProps = {
  biome: TBiome;
  building: TBuildingLinks;
  onToggle: (allowed: boolean) => void;
  onFaceChange: (patch: TFacePatch) => void;
};

/** One biome for one building: the checkbox allows the stroke, the editor sets the biome face. */
const BiomeCard: FC<TBiomeCardProps> = ({ biome, building, onToggle, onFaceChange }) => {
  const biomeFace = building.biomeFaces[biome.id];
  const faces = facesOn(building, biome.id);

  return (
    <div
      className={biomeFace ? "biome-card biome-card--allowed" : "biome-card"}
      style={{ ["--biome-color" as string]: biome.color }}
    >
      <label className="biome-card-head" title={biome.description}>
        <img className="biome-art" src={biome.art} alt="" width={48} height={48} draggable={false} />
        <span className="biome-card-label">
          <span>{biome.label}</span>
          <span className="muted biome-id">{biome.id}</span>
        </span>
        <input type="checkbox" checked={biomeFace !== undefined} onChange={(event) => onToggle(event.target.checked)} />
      </label>

      {biomeFace ? (
        <>
          <FaceEditor face={biomeFace} onChange={onFaceChange} />
          <div className="biome-card-summary muted">
            Кубик из {faces.length} граней: в среднем {formatAverage(averageAmount(faces))} ресурса,{" "}
            {formatAverage(averageToxicity(faces))} токсичности
          </div>
        </>
      ) : (
        <div className="biome-card-summary muted">Строить нельзя</div>
      )}
    </div>
  );
};

export { BiomeCard };
