import { useSignals } from "@preact/signals-react/runtime";
import { getBuilding } from "../../core/buildings";
import { ICONS } from "../../core/icons";
import { plural } from "../../core/toxic-slot";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TCloseRiotAction } from "../../domain/registry";

type TRiotModalRegistrySlice = {
  closeRiotAction: TCloseRiotAction;
};

type TRiotModalProps = {
  registry: TRiotModalRegistrySlice;
};

/**
 * The starvation riot on the player's island, at the start of the clearing
 * phase. The turn waits until "Ясно": a battle stays paused, and without a
 * battle the player is not ready yet.
 */
const RiotModal: FC<TRiotModalProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const report = store.battle.riot.value;

  if (!report) {
    return null;
  }

  const rioterKilled = report.madKilled + report.residentsKilled;
  const razed = report.hits.filter((hit) => hit.razed);
  const damaged = report.hits.filter((hit) => !hit.razed);
  const people = (count: number) => `${count} ${plural(count, "человек", "человека", "человек")}`;

  return (
    <div className="modal-backdrop">
      <div className="panel modal riot-modal">
        <h2 className="modal__title riot-modal__title">
          <Icon src={ICONS.mad} size="m" />
          {"Голодный бунт"}
          <Icon src={ICONS.militia} size="m" />
        </h2>

        <p className="modal__text">
          {`Еды не хватило на ${report.shortfall}. Безумцы и голодные жители вышли на улицы и крушат всё на своём пути. Солдаты встали у них на пути.`}
        </p>

        <div className="riot-modal__stats">
          <div className="riot-modal__stat">
            <span>{"Бунтовщиков"}</span>
            <strong>{`${report.rioters.total}`}</strong>
            <span className="riot-modal__dim">{`безумцев ${report.rioters.mad}, жителей ${report.rioters.residents}`}</span>
          </div>
          <div className="riot-modal__stat">
            <span>{"Погибло солдат"}</span>
            <strong>{`${report.soldiersKilled}`}</strong>
          </div>
          <div className="riot-modal__stat">
            <span>{"Убито бунтовщиков"}</span>
            <strong>{`${rioterKilled}`}</strong>
          </div>
        </div>

        {report.hits.length > 0 ? (
          <div className="riot-modal__section">
            <div className="riot-modal__label">
              {`Разрушено: ${razed.length}`}
              <span className="riot-modal__dim">
                {`повреждено: ${damaged.length}`}
              </span>
            </div>
            <ul className="riot-modal__hits">
              {report.hits.map((hit) => (
                <li key={hit.hexId} className={`riot-modal__hit ${hit.razed ? "riot-modal__hit--razed" : ""}`}>
                  <Icon src={getBuilding(hit.building).art} />
                  <span className="riot-modal__hit-name">
                    {getBuilding(hit.building).label}
                  </span>
                  <span className="riot-modal__hit-hp">
                    {hit.razed ? `${hit.hpBefore} → снесено` : `${hit.hpBefore} → ${hit.hpAfter} / ${hit.max}`}
                  </span>
                </li>
              ))}
            </ul>
            <p className="riot-modal__note">
              {"Снесённые постройки исчезают, на их месте гарь (+10% токсичности). Повреждённые чинятся сами: +25% прочности в конце хода."}
            </p>
          </div>
        ) : (
          <p className="modal__text">
            {"Солдаты разогнали толпу: постройки целы."}
          </p>
        )}

        {report.residentsKilled > 0 ? (
          <p className="riot-modal__note">
            {`Среди жителей погибло ${people(report.residentsKilled)}. Выжившие вернулись к обычной жизни.`}
          </p>
        ) : null}

        <div className="modal__buttons">
          <button type="button" className="button button--primary" onClick={registry.closeRiotAction}>
            {"Ясно"}
          </button>
        </div>
      </div>
    </div>
  );
};

export { RiotModal };
