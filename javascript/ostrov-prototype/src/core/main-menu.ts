import { isQueryFlagOn } from "./guide";

/**
 * The main menu. It is opt-in, like the learn guide: the game opens on the
 * menu only when the address has the `?menu` flag. Without the flag the game
 * opens straight on the island.
 */

const MENU_FLAGS = ["menu"];

/** `?menu` and `?menu=1` open the menu, and `menu=0` (or `false`, `off`) keeps it off. */
const isMenuRequested = (search: string) => isQueryFlagOn(search, MENU_FLAGS);

/**
 * The address without the menu flag. The "Выход" button reloads the page on
 * it, so the game boots the usual way. The other flags and the hash stay.
 */
const addressWithoutMenu = (href: string) => {
  const url = new URL(href);
  MENU_FLAGS.forEach((flag) => url.searchParams.delete(flag));

  return url.toString();
};

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
export { addressWithoutMenu, DIFFICULTIES, isMenuRequested, MENU_FADE_OUT_MS };
