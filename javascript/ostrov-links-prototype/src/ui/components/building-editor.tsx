import { useSignals } from "@preact/signals-react/runtime";
import { BIOMES } from "../../core/biomes";
import { getBuilding } from "../../core/buildings";
import { allowedBiomes, averageAmount, averageToxicity, resourceUses } from "../../core/links";
import { getResource, RESOURCES } from "../../core/resources";
import { useStore } from "../../store/store";
import { formatAverage } from "../format";
import { BiomeCard } from "./biome-card";
import { FaceEditor } from "./face-editor";
import { Icon } from "./icon";
import type { FC } from "react";
import type {
  TAddBaseFaceAction,
  TRemoveBaseFaceAction,
  TSetBiomeAllowedAction,
  TSetYieldsAction,
  TUpdateBaseFaceAction,
  TUpdateBiomeFaceAction,
} from "../../domain/registry";
import type { TYieldResourceId } from "../../core/types";

type TBuildingEditorProps = {
  registry: {
    addBaseFaceAction: TAddBaseFaceAction;
    removeBaseFaceAction: TRemoveBaseFaceAction;
    setBiomeAllowedAction: TSetBiomeAllowedAction;
    setYieldsAction: TSetYieldsAction;
    updateBaseFaceAction: TUpdateBaseFaceAction;
    updateBiomeFaceAction: TUpdateBiomeFaceAction;
  };
};

/**
 * Two questions about one building. The resources section answers "какие
 * ресурсы приносит здание". The biomes section answers "на каком биоме можно
 * строить здание".
 */
const BuildingEditor: FC<TBuildingEditorProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const building = store.derived.selectedBuilding.value;
  const info = getBuilding(building.id);
  const uses = resourceUses(building);
  const allowedCount = allowedBiomes(building).length;

  return (
    <div className="editor">
      <header className="editor-header">
        <Icon src={info.art} size="l" className="editor-art" />
        <div>
          <h1 className="editor-title">{info.label}</h1>
          <div className="muted">id: {building.id}</div>
        </div>
        <label className="yields-field">
          <span className="muted">Основной ресурс</span>
          <span className="yields-select">
            <Icon src={getResource(building.yields).icon} size="m" />
            <select
              value={building.yields}
              onChange={(event) => registry.setYieldsAction(building.id, event.target.value as TYieldResourceId)}
            >
              {RESOURCES.map((resource) => (
                <option key={resource.id} value={resource.id}>
                  {resource.label}
                </option>
              ))}
            </select>
          </span>
        </label>
      </header>

      <section className="section">
        <div className="section-head">
          <h2>Какие ресурсы приносит</h2>
          <span className="muted">
            Здание — это кубик. Базовые грани есть на любом биоме, биом добавляет ещё одну грань.
          </span>
        </div>

        <div className="uses">
          {uses.length === 0 ? (
            <span className="muted">Здание не приносит ни одного ресурса.</span>
          ) : (
            uses.map((use) => {
              const resource = getResource(use.resource);

              return (
                <span key={use.resource} className="use-chip">
                  <Icon src={resource.icon} size="m" />
                  <span>
                    <strong>{resource.label}</strong>
                    <span className="muted">
                      {" "}
                      граней: {use.baseFaces} · биомов: {use.biomes}
                    </span>
                  </span>
                </span>
              );
            })
          )}
        </div>

        <h3 className="subhead">
          Базовые грани
          <span className="muted">
            {" "}
            · в среднем {formatAverage(averageAmount(building.baseFaces))} ресурса и{" "}
            {formatAverage(averageToxicity(building.baseFaces))} токсичности
          </span>
        </h3>
        <ol className="face-list">
          {building.baseFaces.map((item, index) => (
            <li key={index} className="face-row">
              <span className="face-index">{index + 1}</span>
              <FaceEditor
                face={item}
                onChange={(patch) => registry.updateBaseFaceAction(building.id, index, patch)}
              />
              <button
                type="button"
                className="icon-button"
                title="Удалить грань"
                aria-label={`Удалить грань ${index + 1}`}
                onClick={() => registry.removeBaseFaceAction(building.id, index)}
              >
                ✕
              </button>
            </li>
          ))}
        </ol>
        <button type="button" className="button" onClick={() => registry.addBaseFaceAction(building.id)}>
          + Добавить грань
        </button>
      </section>

      <section className="section">
        <div className="section-head">
          <h2>
            Где можно строить <span className="muted">· {allowedCount} из {BIOMES.length}</span>
          </h2>
          <span className="muted">Отметьте биом, чтобы разрешить стройку, и задайте грань, которую биом добавляет.</span>
        </div>

        <div className="biome-grid">
          {BIOMES.map((biome) => (
            <BiomeCard
              key={biome.id}
              biome={biome}
              building={building}
              onToggle={(allowed) => registry.setBiomeAllowedAction(building.id, biome.id, allowed)}
              onFaceChange={(patch) => registry.updateBiomeFaceAction(building.id, biome.id, patch)}
            />
          ))}
        </div>
      </section>
    </div>
  );
};

export { BuildingEditor };
