import { useSignals } from "@preact/signals-react/runtime";
import { ICONS } from "../../../core/icons";
import { SKILLS } from "../../../core/skills";
import { useStore } from "../../../store/store";
import { Icon } from "../icon";
import type { FC, ReactNode } from "react";
import type { TSkillId } from "../../../core/skills";
import type { TArmSkillAction } from "../../../domain/registry";

/** The ink glyph of each skill: a hex split by a crack, with shards flying off. */
const SKILL_GLYPHS: Readonly<Record<TSkillId, ReactNode>> = {
  shatter: (
    <svg className="cleanup-skills__glyph" viewBox="0 0 64 64" aria-hidden="true">
      <path d="M32 6 54 19v26L32 58 10 45V19Z" fill="#8a6a44" stroke="#2a1f14" strokeWidth="4" strokeLinejoin="round" />
      <path d="M32 6 54 19 32 32 10 19Z" fill="#a7c46a" stroke="#2a1f14" strokeWidth="3" strokeLinejoin="round" />
      <path d="M36 4 28 22l9 6-12 13 6 7-8 12" fill="none" stroke="#2a1f14" strokeWidth="5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M36 4 28 22l9 6-12 13 6 7-8 12" fill="none" stroke="#e0644a" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M52 50l6 5M56 40l6 1M8 52l-5 4" stroke="#2a1f14" strokeWidth="3" strokeLinecap="round" />
    </svg>
  ),
};

type TCleanupSkillsRegistrySlice = {
  armSkillAction: TArmSkillAction;
};

type TCleanupSkillsProps = {
  registry: TCleanupSkillsRegistrySlice;
};

/**
 * The active skills of the battle: one button per skill with its hotkey, its
 * cost and a cooldown shade. A picked skill waits for a click on a hex.
 */
const CleanupSkillsBar: FC<TCleanupSkillsProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const hud = store.battle.hud.value;
  const targeting = store.battle.targeting.value;
  const isWaiting = store.derived.isHumanReady.value;

  if (!hud) {
    return null;
  }

  const over = hud.status !== "running";

  return (
    <div className="panel cleanup-panel cleanup-skills">
      <div className="cleanup-skills__mana" title="Мана тратится на активные навыки. Потраченная в бою мана не возвращается.">
        <Icon src={ICONS.mana} size="m" label="Мана" />
        <span className="cleanup-skills__mana-count">
          {`${hud.mana}`}
        </span>
      </div>

      {SKILLS.map((skill) => {
        const cooldown = hud.skillCooldowns[skill.id] ?? 0;
        const share = Math.min(1, cooldown / skill.cooldownSeconds);
        const poor = hud.mana < skill.cost.amount;
        const armed = targeting === skill.id;
        const disabled = over || isWaiting;

        return (
          <button
            key={skill.id}
            type="button"
            className={[
              "cleanup-skills__button",
              armed ? "cleanup-skills__button--armed" : "",
              poor ? "cleanup-skills__button--poor" : "",
              cooldown > 0 ? "cleanup-skills__button--cooling" : "",
            ].join(" ")}
            title={`${skill.label} [${skill.hotkeyLabel}] — ${skill.description}`}
            aria-pressed={armed}
            disabled={disabled}
            onClick={() => registry.armSkillAction(skill.id)}
          >
            {SKILL_GLYPHS[skill.id]}
            <span className="cleanup-skills__shade" style={{ height: `${Math.round(share * 100)}%` }} />
            {cooldown > 0 ? (
              <span className="cleanup-skills__timer">
                {`${Math.ceil(cooldown)}`}
              </span>
            ) : null}
            <kbd className="cleanup-skills__key">
              {skill.hotkeyLabel}
            </kbd>
            <span className="cleanup-skills__cost">
              <Icon src={ICONS.mana} />
              {`${skill.cost.amount}`}
            </span>
          </button>
        );
      })}

      <div className="cleanup-skills__hint">
        {targeting ? "Кликните по гексу в круге. Правый клик или Esc — отмена." : "Разрушить землю: гекс врага или свой токсичный."}
      </div>
    </div>
  );
};

export { CleanupSkillsBar };
