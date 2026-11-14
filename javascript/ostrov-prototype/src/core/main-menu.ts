/**
 * The game has no difficulty setting yet. The menu keeps the choice as a
 * preference in the ui state, and no rule reads it.
 */
type TDifficulty = "easy" | "normal" | "hard";

type TDifficultyOption = {
  readonly id: TDifficulty;
  readonly label: string;
};

const DIFFICULTIES: readonly TDifficultyOption[] = [
  { id: "easy", label: "Лёгкая" },
  { id: "normal", label: "Средняя" },
  { id: "hard", label: "Сложная" },
];

/** The open menu fades out over this time before the game takes the input. */
const MENU_FADE_OUT_MS = 700;

export type { TDifficulty, TDifficultyOption };
export { DIFFICULTIES, MENU_FADE_OUT_MS };
