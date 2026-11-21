import type { FC } from "react";

type TSectionPlaceholderProps = {
  title: string;
  /** What the section will hold. */
  description?: string;
};

/** A section that has no editor yet. */
const SectionPlaceholder: FC<TSectionPlaceholderProps> = ({ title, description }) => {
  return (
    <div className="placeholder">
      <h1>{title}</h1>
      {description ? <p>{description}</p> : null}
      <p className="muted">Раздел пока пустой.</p>
    </div>
  );
};

export { SectionPlaceholder };
