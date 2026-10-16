// The golden traces: the TypeScript sim (../the-vig, the game this project was ported from) played by
// a random driver, every command and teleport it made recorded, and after every tick that did anything
// the state's canonical hash and the tick's events. tools/SimCheck replays the same commands through
// the C# sim and must land on the same hash and the same events, tick by tick.
//
// Run from the repo root (needs ../the-vig with its node_modules):
//   node tools/golden/build.mjs
import { writeFileSync, mkdirSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { Game } from '../../../the-vig/src/sim/Game';
import { Rng, hashStr } from '../../../the-vig/src/engine/rng';
import type { Command } from '../../../the-vig/src/sim/commands';
import { FLOOR_1 } from '../../../the-vig/src/data/floors';
import { COMPUTER_STAND, MAILBOX_STAND, CAR_STAND } from '../../../the-vig/src/data/home';
import { TABLE_BY_ID } from '../../../the-vig/src/data/tables';
import { CHARMS, UPGRADES } from '../../../the-vig/src/data/charms';
import { DRINKS } from '../../../the-vig/src/data/drinks';
import { advise, cardValue, options } from '../../../the-vig/src/sim/tables/twentyone';

/** JSON with keys sorted and nulls left out; numbers as JS prints them (Canon.cs is the twin). */
export function canon(v: unknown): string {
  if (v === null || v === undefined) return 'null';
  if (typeof v === 'number') return String(v);
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (typeof v === 'string') return JSON.stringify(v);
  if (Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  const o = v as Record<string, unknown>;
  const keys = Object.keys(o).filter((k) => o[k] !== undefined && o[k] !== null).sort();
  return '{' + keys.map((k) => JSON.stringify(k) + ':' + canon(o[k])).join(',') + '}';
}

type Action = { tick: number; player: number; tp?: [number, number]; cmd?: Command };

interface Config { seed: number; players: number; home: boolean; upgrades: string[]; unlocked: string[] }

const TABLES = ['bandit', 'twentyone', 'hilo', 'wheel', 'redline', 'deadman'];
const WEIGHT: Record<string, number> = { bandit: 1, twentyone: 2, hilo: 3, wheel: 3, redline: 2, deadman: 1 };

function run(cfg: Config, maxTicks = 140_000) {
  const g = new Game({ seed: cfg.seed, players: cfg.players, home: cfg.home, upgrades: cfg.upgrades as never, unlocked: cfg.unlocked as never });
  const d = new Rng((cfg.seed ^ 0xa5a5a5a5) >>> 0);
  const actions: Action[] = [];
  const lines: string[] = [];
  let acted = false;
  const busy = new Array(cfg.players).fill(0);
  // per player: the table being played and rounds left
  const plan = Array.from({ length: cfg.players }, () => ({ table: '', rounds: 0, walk: 0 }));

  const tp = (pid: number, x: number, z: number) => {
    const p = g.state.players[pid];
    p.x = x;
    p.z = z;
    actions.push({ tick: g.tick, player: pid, tp: [x, z] });
    acted = true;
  };
  const cmd = (pid: number, c: Command) => {
    g.submit(pid, c);
    actions.push({ tick: g.tick, player: pid, cmd: c });
    acted = true;
  };
  const amountFor = (table: string): number | undefined => {
    const b = TABLE_BY_ID.get(table)!.bet;
    const r = d.next();
    if (r < 0.55) return undefined;
    if (r < 0.9) return b.min + b.step * d.int(0, (b.max - b.min) / b.step);
    if (r < 0.95) return b.min + d.int(0, b.max - b.min); // rounds to a step
    return d.chance(0.5) ? b.max * 3 : -50; // refused
  };

  const decide = (pid: number) => {
    const s = g.state;
    const p = s.players[pid];
    const pl = plan[pid];
    switch (s.phase) {
      case 'home': {
        const r = d.next();
        if (r < 0.35) {
          tp(pid, COMPUTER_STAND.x + d.range(-0.5, 0.5), COMPUTER_STAND.z + d.range(-0.5, 0.5));
          const pool = [...CHARMS.map((c) => c.id), ...UPGRADES.map((u) => u.id), 'nothing'];
          cmd(pid, { t: 'buy', item: d.pick(pool) });
          busy[pid] = g.tick + d.int(10, 90);
        } else if (r < 0.55) {
          tp(pid, MAILBOX_STAND.x, MAILBOX_STAND.z + d.range(-0.4, 0.4));
          cmd(pid, { t: 'pickup' });
          busy[pid] = g.tick + d.int(30, 400);
        } else if (r < 0.75) {
          pl.walk = d.int(20, 120);
          busy[pid] = g.tick;
        } else if (r < 0.8) {
          cmd(pid, { t: d.pick(['ready', 'pay', 'sit', 'callit'] as const) } as Command);
          busy[pid] = g.tick + 5;
        } else {
          tp(pid, CAR_STAND.x + d.range(-0.6, 0.6), CAR_STAND.z);
          cmd(pid, { t: 'drive' });
          busy[pid] = g.tick + 30;
        }
        return;
      }
      case 'floor': {
        if (p.ghost) {
          // a ghost wanders and is refused
          if (d.chance(0.1)) cmd(pid, { t: 'sit', table: d.pick(TABLES) });
          pl.walk = d.int(30, 200);
          return;
        }
        const t = s.tables;
        if (p.seat && pl.rounds > 0) {
          const id = p.seat;
          if (t[id].pending) return;
          const def = TABLE_BY_ID.get(id)!;
          if (id === 'twentyone') {
            const h = t.twentyone.hand;
            if (h && h.stage === 'play') {
              const can = options(h);
              const call = d.chance(0.8) ? advise(h.hands[h.active].cards, cardValue(h.dealer[0]), can.double, can.split) : d.pick(['hit', 'stand', 'double', 'split', 'fold']);
              cmd(pid, { t: 'call', table: id, call });
              busy[pid] = g.tick + d.int(8, 40);
              return;
            }
            pl.rounds--;
            cmd(pid, { t: 'bet', table: id, amount: amountFor(id) });
            busy[pid] = g.tick + d.int(10, 40);
            return;
          }
          pl.rounds--;
          const call = id === 'hilo' ? d.pick(['hi', 'lo']) : id === 'redline' ? d.pick(['red', 'black']) : undefined;
          const a = amountFor(id);
          cmd(pid, call ? { t: 'call', table: id, call, amount: a } : { t: 'spin', table: id, amount: a });
          busy[pid] = g.tick + Math.round(def.roundS * 60 * d.range(0.4, 1.2));
          return;
        }
        if (p.seat) {
          cmd(pid, { t: 'stand' });
          busy[pid] = g.tick + d.int(5, 30);
          return;
        }
        // made it with room to spare: wait out the night, or call it at his table
        if (s.bank >= s.quota + 400 && d.chance(0.8)) {
          if (cfg.players === 1 && d.chance(0.3)) {
            const c = FLOOR_1.collector.stand;
            tp(pid, c.x, c.z);
            cmd(pid, { t: 'callit' });
          } else pl.walk = d.int(60, 600);
          busy[pid] = g.tick + d.int(60, 600);
          return;
        }
        const r = d.next();
        if (r < 0.7) {
          const table = d.weighted(TABLES, (x) => WEIGHT[x]);
          const pt = FLOOR_1.tables.find((x) => x.table === table)!;
          const off = d.chance(0.1) ? 2.2 : d.range(0, 0.8);
          tp(pid, pt.stand.x + off, pt.stand.z);
          cmd(pid, { t: 'sit', table });
          pl.rounds = d.int(1, 8);
          busy[pid] = g.tick + d.int(20, 120);
        } else if (r < 0.78) {
          tp(pid, FLOOR_1.barStand.x + d.range(-1, 1), FLOOR_1.barStand.z + d.range(-2, 2));
          cmd(pid, { t: 'order', drink: d.chance(0.9) ? d.pick(DRINKS).id : 'water' });
          busy[pid] = g.tick + d.int(30, 120);
        } else if (r < 0.84) {
          tp(pid, FLOOR_1.cageStand.x, FLOOR_1.cageStand.z + d.range(-1, 1));
          if (cfg.players > 1 && s.players.some((o) => o.ghost)) cmd(pid, { t: 'buyback', target: s.players.findIndex((o) => o.ghost) });
          else cmd(pid, { t: 'buy', item: d.pick([...CHARMS.map((c) => c.id), ...UPGRADES.map((u) => u.id)]) });
          busy[pid] = g.tick + d.int(30, 120);
        } else if (r < 0.97) {
          pl.walk = d.int(30, 240);
        } else {
          // noise: a command out of place
          cmd(pid, d.pick<Command>([{ t: 'pay' }, { t: 'fortune' }, { t: 'pickup' }, { t: 'spin', table: 'hilo' }, { t: 'activate', circuit: 3 }, { t: 'ready' }]));
          busy[pid] = g.tick + 10;
        }
        return;
      }
      case 'choice':
        if (d.chance(0.05)) return;
        cmd(pid, { t: d.chance(0.5) ? 'pay' : 'gamble' });
        busy[pid] = g.tick + d.int(20, 90);
        return;
      case 'circuit': {
        const c = s.circuit!;
        if (c.done || c.turn !== 'team') return;
        const up = c.levers.map((l, i) => (l.pulled ? -1 : i)).filter((i) => i >= 0);
        const r = d.next();
        if (r < 0.15 && c.traces[pid] > 0) cmd(pid, { t: 'inspect', circuit: d.pick(up) });
        else if (r < 0.2) cmd(pid, { t: 'reroute', a: 0, b: 1 });
        else if (r < 0.23) cmd(pid, { t: 'activate', circuit: d.chance(0.5) ? 99 : -1 });
        else cmd(pid, { t: 'activate', circuit: d.pick(up) });
        busy[pid] = g.tick + d.int(20, 120);
        return;
      }
      case 'wheel':
        if (s.wheel && s.wheel.slot === null) cmd(pid, { t: 'fortune' });
        busy[pid] = g.tick + d.int(20, 120);
        return;
      case 'summary':
        cmd(pid, { t: 'ready' });
        busy[pid] = g.tick + d.int(30, 200);
        return;
    }
  };

  while (g.tick < maxTicks && g.state.phase !== 'over') {
    acted = false;
    for (let pid = 0; pid < cfg.players; pid++) {
      const pl = plan[pid];
      if (pl.walk > 0) {
        // walk: a fresh direction every 12 ticks, into walls and tables as often as not
        if (pl.walk % 12 === 0 || pl.walk === 1) {
          const stop = pl.walk === 1;
          cmd(pid, { t: 'move', mx: stop ? 0 : d.int(-1200, 1200), mz: stop ? 0 : d.int(-1200, 1200), yaw: d.int(-7000, 7000), run: d.chance(0.4) });
        }
        pl.walk--;
        continue;
      }
      if (g.tick >= busy[pid]) decide(pid);
    }
    g.step();
    const ev = g.drainEvents();
    if (ev.length || acted || g.tick % 600 === 0) {
      const st = canon(g.state);
      lines.push(`${g.tick - 1}|${g.rng.s}|${(hashStr(st) >>> 0).toString(16)}|${canon(ev)}`);
    }
  }
  return { config: cfg, ticks: g.tick, actions, lines, end: { phase: g.state.phase, night: g.state.night, bank: g.state.bank, result: g.state.result } };
}

const out = 'tools/golden/traces';
mkdirSync(out, { recursive: true });
const configs: Config[] = [];
for (let i = 0; i < 16; i++) {
  const seed = (1000 + i * 7919) >>> 0;
  configs.push({
    seed,
    players: i % 4 === 3 ? 2 : 1,
    home: i % 2 === 0,
    upgrades: i % 5 === 1 ? ['bank500'] : i % 5 === 2 ? ['slot4', 'hilo20'] : [],
    unlocked: i % 3 === 0 ? ['insulated'] : [],
  });
}
for (const cfg of configs) {
  const r = run(cfg);
  const file = `${out}/seed-${cfg.seed}.json.gz`;
  writeFileSync(file, gzipSync(JSON.stringify(r)));
  console.log(`${file}: ${r.ticks} ticks, ${r.actions.length} actions, ${r.lines.length} lines, ended ${r.end.phase} night ${r.end.night} bank ${r.end.bank}`);
}
