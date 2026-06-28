import raw from "./commands.txt?raw";

/** Full Commands.txt text, bundled (Block 10 viewer reads this, not disk).
 * Includes BotTaunt commands such as lbtv_bot_taunt, lbtv_bot_chat, and lbtv_bot_rivalry.
 * Includes cfg alias knife commands such as lbtv_knife_hot, lbtv_knife_rdm, and lbtv_knife_all.
 */
export const COMMANDS_TXT: string = raw;

export type Team = {
  index: number;
  name: string;
  /** Full `bot_add_ct ...` console line. */
  ct: string;
  /** Full `bot_add_t ...` console line. */
  t: string;
};

/** Parse the "ADD TEAMS" .. "COORDINATED BUY" region into teams. */
function parseTeams(text: string): Team[] {
  const lines = text.split(/\r?\n/);
  const start = lines.findIndex((l) => l.trim().toUpperCase() === "ADD TEAMS");
  const endRel = lines
    .slice(start + 1)
    .findIndex((l) => l.trim().toUpperCase() === "COORDINATED BUY");
  const end = endRel === -1 ? lines.length : start + 1 + endRel;
  const region = start === -1 ? [] : lines.slice(start + 1, end);

  const teams: Team[] = [];
  let cur: Team | null = null;
  const header = /^\s*(\d+)\.\s*(.+?)\s*$/;

  for (const line of region) {
    const trimmed = line.trim();
    const m = trimmed.match(header);
    if (m) {
      cur = { index: parseInt(m[1], 10), name: m[2], ct: "", t: "" };
      teams.push(cur);
      continue;
    }
    if (!cur) continue;
    if (trimmed.startsWith("bot_add_ct")) cur.ct = trimmed;
    else if (trimmed.startsWith("bot_add_t")) cur.t = trimmed;
  }

  return teams.filter((t) => t.name && (t.ct || t.t));
}

export const TEAMS: Team[] = parseTeams(raw);
