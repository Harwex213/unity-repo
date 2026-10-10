import type { TResourceId } from "./types";

/**
 * Active skills of the cleanup phase. A skill is cast on one hex of any island
 * in reach and is paid with a resource of the player's pool. The battle keeps
 * the cooldowns; the player keeps the pool, so mana spent in battle is gone
 * after it.
 *
 * Every skill is a definition here. The sim (`cleanup-sim.ts`) applies its
 * effect by id, so a new skill is one entry here and one case there.
 */

type TSkillId = "shatter";

type TSkill = {
  readonly id: TSkillId;
  readonly label: string;
  /** One line for the tooltip of the skills bar. */
  readonly description: string;
  readonly cost: { readonly resource: TResourceId; readonly amount: number };
  readonly cooldownSeconds: number;
  /** `KeyboardEvent.code` of the hotkey, and the label the bar shows for it. */
  readonly hotkey: string;
  readonly hotkeyLabel: string;
  /** How far past the rim of the player's island the skill reaches, in world units. */
  readonly reach: number;
};

const SKILLS: readonly TSkill[] = [
  {
    id: "shatter",
    label: "Разрушить землю",
    description:
      "Разрушает гекс острова: врага или свой, например токсичный. Части острова, которые потеряли связь с ядром, откалываются и падают. Гекс твердыни разрушить нельзя.",
    cost: { resource: "mana", amount: 2 },
    cooldownSeconds: 6,
    hotkey: "KeyQ",
    hotkeyLabel: "Q",
    reach: 700,
  },
];

const SKILL_BY_ID = new Map(SKILLS.map((skill) => [skill.id, skill]));

const getSkill = (id: TSkillId) => {
  const skill = SKILL_BY_ID.get(id);
  if (!skill) {
    throw new Error(`Unknown skill: ${id}`);
  }

  return skill;
};

export type { TSkill, TSkillId };
export { getSkill, SKILLS };
