# Xeno Variant Design Summary

## Core System
- **4 xeno types**, each gets **1 variant** per run
- Variants evolve **once** during a campaign (8-12 missions)
- Random spawns - no designed combos
- Auto-chess constraints: minimal player control during combat

## Base Stats Reference
| Type | HP | Speed | Power | Role |
|------|-----|-------|-------|------|
| Scout | 10 | 24 | 6 | Fast melee swarm |
| Sleeper | 20 | 20 | 7 | Ambush assassin |
| Mauler | 30 | 22 | 8 | Tank/bruiser |
| Spitter | 20 | 25 | 2 | Ranged only |

**Key mechanic**: Speed affects action time (each +1 speed = -0.2s per action)

---

## Scout Variants

### 分裂 (Splitting)
- Spawns 2 Scouts at 50% HP each when killed
- **Speed stacking**: +1 speed per visible Scout
- Creates exponential swarm pressure

### 献祭 (Sacrifice)  
- On death: allies on same tile get +2 speed, +2 melee damage for 5s
- Buff doesn't stack from multiple deaths, but refreshes duration
- Turns Scout deaths into tactical advantage

### 猎手 (Hunter)
- Actively seeks nearest target
- More aggressive targeting AI

**Design notes**: 
- Clear escalation (分裂 swarms, 献祭 empowers survivors)
- **Question for team**: Does 献祭 buff refresh duration or ignore subsequent deaths?

---

## Sleeper Variants

### 潜伏 (Stealth)
- Undetectable except by vision
- +2 speed when in rooms (encourages ambushes)

### 吸血 (Lifesteal)
- Heals for half of the damage dealt
- Only works when the enemy is above 50% HP

### 窒息 (Suffocation)
- Stuns enemies for 2s on first melee engagement
- Once per Sleeper

### 出血 (Bleed)
- Melee hits cause 3 damage over 30s (1 per 10s)
- Stacks up to 3 times
- Can be removed if healed

### 感电皮肤 (Electrified Skin)
- Received melee hits deal 2 electric damage back to attacker

**Design notes**:
- Anti-synthetic theme maintains biological horror identity
- 2s stun = ~1 extra hit (balanced for auto-chess)
- **Question for team**: Does 潜伏 change the "FollowClosestVisible" behavior?

---

## Mauler Variants

### 重甲 (Heavy Armor)
- All damage -1
- If damage would be 1, 50% chance to negate completely

### 孵化者 (Incubator)
- Spawns 1 Scout on death
- Can chain with Scout's 献祭 naturally (not designed combo)

### 恐怖外表 (Terrifying Presence)
- All visible enemies must target this Mauler
- Forces focus fire, protects other xenos

**Design notes**: Most polished variant set, no issues

---

## Spitter Variants

### 灵敏 (Agile)
- Changes retreat → kiting fire (shoot while retreating)
- -10 accuracy (50% → 40%)
- Mobile harasser role

### 突击 (Assault)
- Optimal range: close instead of far
- Changes retreat → rush-down shooting (shoot while advancing)
- +5 melee damage (2 → 7 power in melee)
- Becomes melee hybrid

### 神射手 (Sharpshooter)
- +50 accuracy (50% → 100%)
- -2 speed (25 → 23)
- Stand-and-shoot specialist

---

## Design Strengths
✓ Clear identity per variant (swarm/assassin/tank/ranged)  
✓ Respects auto-chess constraints (no acid pools, no complex combos)  
✓ Natural escalation through evolution system  
✓ Avoid punishing RNG (no designed combos needed)