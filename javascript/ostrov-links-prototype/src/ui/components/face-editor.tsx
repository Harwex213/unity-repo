import toxicityIcon from "../../assets/icons/toxicity.png";
import { getResource, RESOURCES } from "../../core/resources";
import { Icon } from "./icon";
import type { ChangeEvent, FC } from "react";
import type { TFacePatch } from "../../core/links";
import type { TFace, TYieldResourceId } from "../../core/types";

type TFaceEditorProps = {
  face: TFace;
  onChange: (patch: TFacePatch) => void;
};

/** An empty field changes nothing, so the user can clear it and type a new number. */
const readNumber = (event: ChangeEvent<HTMLInputElement>) => {
  return event.target.value === "" ? undefined : Number(event.target.value);
};

/** One die face: which resource, how much, and how much toxicity it leaves. */
const FaceEditor: FC<TFaceEditorProps> = ({ face, onChange }) => {
  const resource = getResource(face.resource);

  return (
    <div className="face-editor">
      <label className="face-field face-field--resource">
        <Icon src={resource.icon} label={resource.label} size="m" />
        <select
          value={face.resource}
          aria-label="Ресурс"
          onChange={(event) => onChange({ resource: event.target.value as TYieldResourceId })}
        >
          {RESOURCES.map((item) => (
            <option key={item.id} value={item.id}>
              {item.label}
            </option>
          ))}
        </select>
      </label>
      <label className="face-field" title="Сколько ресурса даёт грань">
        <span className="face-field-label">×</span>
        <input
          type="number"
          min={0}
          step={1}
          value={face.amount}
          aria-label="Количество"
          onChange={(event) => onChange({ amount: readNumber(event) })}
        />
      </label>
      <label className="face-field" title="Сколько токсичности оставляет грань">
        <Icon src={toxicityIcon} label="Токсичность" />
        <input
          type="number"
          min={0}
          step={1}
          value={face.toxicity}
          aria-label="Токсичность"
          onChange={(event) => onChange({ toxicity: readNumber(event) })}
        />
      </label>
    </div>
  );
};

export { FaceEditor };
