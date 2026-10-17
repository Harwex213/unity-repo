import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useRef } from "react";
import { populationOf } from "../../core/population";
import { getResource } from "../../core/resources";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TResourceId } from "../../core/types";
import type { TPointerAnchor } from "../../store/ui-state";
import type { TSetHudAnchorsAction } from "../../domain/registry";

type TResourcesPanelRegistrySlice = {
  setHudAnchorsAction: TSetHudAnchorsAction;
};

type TResourcesPanelProps = {
  registry: TResourcesPanelRegistrySlice;
};

/**
 * Two panels, two rows each. Every row lists resource ids from left to right.
 * Toxicity has its own meter, so it is not listed here. The population cell
 * shows the idle people; its hint splits the whole population into idle,
 * workers and army.
 */
const PANEL_LAYOUT: readonly (readonly (readonly TResourceId[])[])[] = [
  [
    ["food", "wood", "stone"],
    ["population", "mad", "hammers"],
  ],
  [
    ["power", "scouting"],
    ["science", "mana"],
  ],
];

/** Rounds the way the reference HUD does: one decimal, no trailing zero. */
const formatAmount = (amount: number) => {
  return Number.isInteger(amount) ? String(amount) : amount.toFixed(1);
};

/**
 * The resources panels only display data, as the spec says. They also report
 * where each icon sits, because the tax phase flies the motes to them.
 */
const ResourcesPanel: FC<TResourcesPanelProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const pool = store.derived.humanPlayer.value?.resources;
  const powerPlanned = store.derived.powerPlanned.value;
  const iconRefs = useRef(new Map<TResourceId, HTMLElement>());

  useEffect(() => {
    const measure = () => {
      const anchors: Partial<Record<TResourceId, TPointerAnchor>> = {};

      iconRefs.current.forEach((node, id) => {
        const box = node.getBoundingClientRect();
        anchors[id] = { x: box.left + box.width / 2, y: box.top + box.height / 2 };
      });

      registry.setHudAnchorsAction(anchors);
    };

    measure();
    window.addEventListener("resize", measure);

    return () => {
      window.removeEventListener("resize", measure);
    };
  }, [registry]);

  const player = store.derived.humanPlayer.value;
  if (!pool || !player) {
    return null;
  }

  const people = populationOf(player);
  const setIconRef = (id: TResourceId) => (node: HTMLElement | null) => {
    if (node) {
      iconRefs.current.set(id, node);
    } else {
      iconRefs.current.delete(id);
    }
  };

  return (
    <div className="resources-panels" data-tutorial="tax">
      {PANEL_LAYOUT.map((rows, panelIndex) => (
        <div key={panelIndex} className="panel resources-panel">
          {rows.flatMap((ids, rowIndex) => ids.map((id, columnIndex) => {
            const resource = getResource(id);
            // The first column of the first panel stands at the left edge of the screen.
            const edgeClass = panelIndex === 0 && columnIndex === 0 ? " resource--left-edge" : "";

            return (
              <div
                key={resource.id}
                className={`resource has-hint resource--${resource.kind} resource--${resource.id}${edgeClass}`}
                style={{ gridRow: rowIndex + 1, gridColumn: columnIndex + 1 }}
              >
                <span className="resource__icon" ref={setIconRef(resource.id)}>
                  <Icon src={resource.icon} size="m" />
                </span>

                {/* The population shows the idle people: the ones the player can still send somewhere. */}
                <span className="resource__amount">
                  {resource.id === "population" ? people.idle : formatAmount(pool[resource.id])}
                </span>

                {/* Power picked in the tax phase leaves the pool when the phase ends. */}
                {resource.id === "power" && powerPlanned > 0 ? (
                  <span className="resource__pending">
                    {`−${powerPlanned}`}
                  </span>
                ) : null}

                <span className="hint">
                  <span className="hint__title">
                    {resource.label}
                  </span>

                  <span className="hint__row">
                    {resource.feeds}
                  </span>

                  {resource.id === "population" ? (
                    <>
                      <span className="hint__row">
                        {`Свободно: ${people.idle}`}
                      </span>
                      <span className="hint__row">
                        {`Работают: ${people.workers}`}
                      </span>
                      <span className="hint__row">
                        {`Служат: ${people.army}`}
                      </span>
                    </>
                  ) : null}
                </span>
              </div>
            );
          }))}
        </div>
      ))}

    </div>
  );
};

export { ResourcesPanel };
