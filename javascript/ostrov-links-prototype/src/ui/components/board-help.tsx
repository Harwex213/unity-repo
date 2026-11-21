import type { FC, ReactNode } from "react";

type TBoardHelpProps = {
  title: string;
  /** Extra blocks above the controls, for example a legend. */
  children?: ReactNode;
  /** Board-specific controls, added after the common ones. */
  extraControls?: readonly string[];
};

const COMMON_CONTROLS = [
  "Колесо мыши или щипок на тачпаде — зум вокруг курсора.",
  "Перетаскивание поля — сдвиг.",
  "Перетаскивание карточки — новое место. Раскладка сохраняется в git-файл в data/.",
  "Двойной клик по пустому полю или «Вписать» — показать всё.",
];

/** The sidebar of a board: what is on it and how to move around. */
const BoardHelp: FC<TBoardHelpProps> = ({ title, children, extraControls = [] }) => {
  return (
    <div className="board-help">
      <div className="sidebar-title">{title}</div>
      {children}
      <ul className="board-help-list">
        {[...COMMON_CONTROLS, ...extraControls].map((text) => (
          <li key={text}>{text}</li>
        ))}
      </ul>
    </div>
  );
};

export { BoardHelp };
