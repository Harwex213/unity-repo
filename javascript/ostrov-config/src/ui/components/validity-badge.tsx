import type { FC } from "react";
import type { TValidation } from "../../core/types";

type TValidityBadgeProps = {
  validation: TValidation;
};

const LABELS: Record<TValidation["status"], string> = {
  "valid": "valid",
  "invalid": "invalid",
  "missing-schema": "no schema",
};

const ValidityBadge: FC<TValidityBadgeProps> = ({ validation }) => {
  return <span className={`badge badge-${validation.status}`}>{LABELS[validation.status]}</span>;
};

export { ValidityBadge };
