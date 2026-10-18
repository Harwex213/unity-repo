import { ICONS } from "../../core/icons";
import { getResource } from "../../core/resources";
import { effectiveYield } from "../../core/tax";
import { Icon } from "./icon";
import type { FC, ReactNode } from "react";
import type { TAlwaysYield, TFace, THex, TResourceId } from "../../core/types";

/**
 * The look of a face chip:
 * - plain: a face of the die that every copy of the building has;
 * - biome: the face that the biome adds, a dashed teal rim;
 * - always: a yield that comes every turn without a roll, a gold rim and a ∞ badge.
 */
type TFaceKind = "plain" | "biome" | "always";

const BIOME_TITLE = "Зависит от биома";
const ALWAYS_TITLE = "Каждый ход";

type TFaceChipProps = {
  kind?: TFaceKind;
  resource: TResourceId;
  /** The text after the resource icon: "3", "3→2", "+1" or "?". */
  amount: ReactNode;
  /** The text after the toxicity icon. Left out for a yield with no toxicity. */
  toxicity?: ReactNode;
  /** The rolled or the chosen face. The highlight works on any kind. */
  isSelected?: boolean;
  /** Replaces the default title of the kind. */
  title?: string;
};

/**
 * One face as a chip: the resource and the amount, then the toxicity. Every
 * place that shows a die draws its faces with this chip.
 */
const FaceChip: FC<TFaceChipProps> = ({ kind = "plain", resource, amount, toxicity, isSelected = false, title }) => {
  const info = getResource(resource);
  const defaultTitle = kind === "biome" ? BIOME_TITLE : kind === "always" ? ALWAYS_TITLE : undefined;

  return (
    <span className={`face face--${kind} ${isSelected ? "face--selected" : ""}`} title={title ?? defaultTitle}>
      <span className="face__yield">
        <Icon src={info.icon} label={info.label} />
        {amount}
      </span>

      {toxicity === undefined ? null : (
        <span className="face__toxicity">
          <Icon src={ICONS.toxicity} label="Токсичность" />
          {toxicity}
        </span>
      )}

      {kind === "always" ? (
        <span className="face__badge" aria-hidden="true">
          {"∞"}
        </span>
      ) : null}
    </span>
  );
};

type TFaceGridProps = {
  children: ReactNode;
  className?: string;
};

/** The chips two per row. An odd last chip takes the middle of its row. */
const FaceGrid: FC<TFaceGridProps> = ({ children, className = "" }) => {
  return <span className={`face-grid ${className}`}>{children}</span>;
};

/** The chip of a yield that comes every turn. */
const AlwaysChip: FC<{ item: TAlwaysYield }> = ({ item }) => {
  return <FaceChip kind="always" resource={item.resource} amount={`+${item.amount}`} />;
};

type TDieFacesProps = {
  faces: readonly TFace[];
  /** Index of the biome face. -1 when there is none. */
  biomeFaceIndex: number;
  /** The biome of the hex, for the title of the biome face. */
  biomeLabel: string;
  /** When given, each face shows what the hex's toxicity leaves of it. */
  hex?: THex | null;
  /** Yields that come every turn, drawn after the faces. */
  alwaysYields?: readonly TAlwaysYield[];
  /** Index of the face to highlight: the rolled or the chosen one. */
  selectedIndex?: number;
  className?: string;
};

/**
 * The faces of one die on a hex, then the yields that come every turn. The
 * hover tooltip, the build preview and the hex panel draw a die this way.
 */
const DieFaces: FC<TDieFacesProps> = ({
  faces,
  biomeFaceIndex,
  biomeLabel,
  hex = null,
  alwaysYields = [],
  selectedIndex = -1,
  className,
}) => {
  return (
    <FaceGrid className={className}>
      {faces.map((item, index) => {
        const paid = hex ? effectiveYield(item, hex) : item.amount;
        const isBiomeFace = index === biomeFaceIndex;

        return (
          <FaceChip
            key={`${item.resource}-${item.amount}-${item.toxicity}-${index}`}
            kind={isBiomeFace ? "biome" : "plain"}
            resource={item.resource}
            amount={paid === item.amount ? item.amount : `${item.amount}→${paid}`}
            toxicity={item.toxicity}
            isSelected={index === selectedIndex}
            title={isBiomeFace ? `${BIOME_TITLE}: «${biomeLabel}»` : undefined}
          />
        );
      })}

      {alwaysYields.map((item) => (
        <AlwaysChip key={`always-${item.resource}`} item={item} />
      ))}
    </FaceGrid>
  );
};

export type { TFaceKind };
export { DieFaces, FaceChip, FaceGrid };
