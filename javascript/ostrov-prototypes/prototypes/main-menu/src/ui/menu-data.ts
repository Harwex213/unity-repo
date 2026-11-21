/** The entries of the menu of the Ostrov prototype, in the same order. */
type TMenuEntry = {
  readonly id: string;
  /** The small gold line, like "Глава первая". */
  readonly label: string;
  readonly title: string;
};

const MENU_ENTRIES: readonly TMenuEntry[] = [
  { id: "prologue", label: "Пролог", title: "Обучение" },
  { id: "chapter-one", label: "Глава первая", title: "Новая игра" },
  { id: "chronicle", label: "Летопись", title: "Фракции" },
  { id: "archive", label: "Архив", title: "Технологии" },
  { id: "epilogue", label: "Эпилог", title: "Авторы" },
  { id: "exit", label: "Покинуть", title: "Выход" },
];

const DIFFICULTIES = ["Лёгкая", "Средняя", "Сложная"] as const;

export type { TMenuEntry };
export { DIFFICULTIES, MENU_ENTRIES };
