import { useSignals } from "@preact/signals-react/runtime";
import toxicityIcon from "../../assets/icons/toxicity.png";
import { BIOMES } from "../../core/biomes";
import { BUILDINGS } from "../../core/buildings";
import { buildingsOnBiome, resourceUses } from "../../core/links";
import { getResource, RESOURCES } from "../../core/resources";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TSelectBuildingAction, TSetBiomeAllowedAction } from "../../domain/registry";

type TMatrixViewProps = {
  registry: {
    selectBuildingAction: TSelectBuildingAction;
    setBiomeAllowedAction: TSetBiomeAllowedAction;
  };
};

/**
 * Every link at once. The first table answers "на каком биоме можно строить
 * здание": a click on a cell allows or forbids the building there. The second
 * table answers "какие ресурсы приносит здание".
 */
const MatrixView: FC<TMatrixViewProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const links = store.links.links.value;
  const issues = store.derived.issues.value;

  return (
    <div className="matrix-view">
      <section className="section">
        <div className="section-head">
          <h2>Здания × биомы</h2>
          <span className="muted">
            Клик по клетке разрешает или запрещает стройку. Клетка показывает грань, которую добавляет биом.
          </span>
        </div>

        <div className="table-scroll">
          <table className="matrix">
            <thead>
              <tr>
                <th className="matrix-corner" />
                {BIOMES.map((biome) => (
                  <th key={biome.id} className="matrix-biome" title={biome.description}>
                    <img src={biome.art} alt="" width={32} height={32} draggable={false} />
                    <span>{biome.label}</span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {BUILDINGS.map((info) => {
                const building = links[info.id];

                return (
                  <tr key={info.id}>
                    <th className="matrix-building">
                      <button type="button" className="link-button" onClick={() => registry.selectBuildingAction(info.id)}>
                        <Icon src={info.art} size="m" />
                        {info.label}
                      </button>
                    </th>
                    {BIOMES.map((biome) => {
                      const biomeFace = building.biomeFaces[biome.id];
                      const action = biomeFace ? "Запретить" : "Разрешить";

                      return (
                        <td key={biome.id} className={biomeFace ? "cell cell--on" : "cell"}>
                          <button
                            type="button"
                            className="cell-button"
                            title={`${action}: ${info.label} на биоме «${biome.label}»`}
                            aria-label={`${action}: ${info.label} на биоме «${biome.label}»`}
                            aria-pressed={biomeFace !== undefined}
                            onClick={() => registry.setBiomeAllowedAction(info.id, biome.id, !biomeFace)}
                          >
                            {biomeFace ? (
                              <span className="cell-face">
                                <span>
                                  <Icon src={getResource(biomeFace.resource).icon} />
                                  {biomeFace.amount}
                                </span>
                                {biomeFace.toxicity > 0 ? (
                                  <span className="cell-toxicity">
                                    <Icon src={toxicityIcon} />
                                    {biomeFace.toxicity}
                                  </span>
                                ) : null}
                              </span>
                            ) : null}
                          </button>
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr>
                <th className="matrix-building muted">Зданий на биоме</th>
                {BIOMES.map((biome) => {
                  const count = buildingsOnBiome(links, biome.id).length;

                  return (
                    <td key={biome.id} className={count === 0 ? "cell-count cell-count--empty" : "cell-count"}>
                      {count}
                    </td>
                  );
                })}
              </tr>
            </tfoot>
          </table>
        </div>
      </section>

      <section className="section">
        <div className="section-head">
          <h2>Здания × ресурсы</h2>
          <span className="muted">
            Клетка: сколько базовых граней и сколько граней биомов дают ресурс. Грани правятся в карточке здания.
          </span>
        </div>

        <div className="table-scroll">
          <table className="matrix">
            <thead>
              <tr>
                <th className="matrix-corner" />
                {RESOURCES.map((resource) => (
                  <th key={resource.id} className="matrix-resource">
                    <Icon src={resource.icon} size="m" />
                    <span>{resource.label}</span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {BUILDINGS.map((info) => {
                const building = links[info.id];
                const uses = resourceUses(building);

                return (
                  <tr key={info.id}>
                    <th className="matrix-building">
                      <button type="button" className="link-button" onClick={() => registry.selectBuildingAction(info.id)}>
                        <Icon src={info.art} size="m" />
                        {info.label}
                      </button>
                    </th>
                    {RESOURCES.map((resource) => {
                      const use = uses.find((item) => item.resource === resource.id);
                      const isMain = building.yields === resource.id;

                      return (
                        <td
                          key={resource.id}
                          className={use ? "cell cell--on cell--static" : "cell cell--static"}
                          title={isMain ? "Основной ресурс здания" : undefined}
                        >
                          {use ? (
                            <span className="cell-use">
                              {isMain ? <strong>★ </strong> : null}
                              {use.baseFaces} / {use.biomes}
                            </span>
                          ) : isMain ? (
                            <strong title="Основной ресурс, но ни одна грань его не даёт">★</strong>
                          ) : null}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      <section className="section">
        <div className="section-head">
          <h2>Проверка</h2>
        </div>
        {issues.length === 0 ? (
          <div className="ok-text">Проблем не найдено.</div>
        ) : (
          <ul className="issues">
            {issues.map((issue) => (
              <li key={issue}>{issue}</li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
};

export { MatrixView };
