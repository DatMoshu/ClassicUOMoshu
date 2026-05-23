// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IActiveBuffSource backed by the live player's
// BuffIcons dict. Classifies each BuffIconType into a BuffArchetype using the legacy
// 12-bucket switch (preserved verbatim from BuffParticleEffects.ClassifyBuff).

using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IActiveBuffSource"/>. Pulls the current player from
    /// <c>Client.Game.UO.World.Player</c> each query (keeping the source ignorant of
    /// scene lifecycle). Returns null when the player or buff list is missing — the
    /// service handles that as "no active buffs".
    /// </summary>
    internal sealed class LegacyActiveBuffSource : IActiveBuffSource
    {
        private readonly List<ActiveBuffEntry> _scratch = new(capacity: 32);

        public IReadOnlyCollection<ActiveBuffEntry> GetActiveBuffs()
        {
            var player = ClassicUO.Client.Game.UO?.World?.Player;
            var buffs = player?.BuffIcons;
            if (buffs == null || buffs.Count == 0) return null;

            _scratch.Clear();
            foreach (var kv in buffs)
            {
                BuffIconType type = kv.Key;
                // ClassifyBuff returns the LEGACY enum (it's defined in the legacy namespace
                // and switch arms can't use the file-level using alias); cast through int to
                // the canonical Effects enum. Values are bit-identical (locked by parity test).
                var arc = (ClassicUO.Renderer.Effects.BuffArchetype)(int)ClassifyBuff(type);
                _scratch.Add(new ActiveBuffEntry(arc, (ulong)(int)type));
            }
            return _scratch;
        }

        // ===== Legacy 12-bucket classification (preserved verbatim) =====
        // Multi-label switch — categories that override others (form-shift, stealth,
        // lightning) sit before the broader stat/defense buckets so they win on overlap.

        public static BuffArchetype ClassifyBuff(BuffIconType t) => t switch
        {
            // ----- Form shifts -----
            BuffIconType.AnimalForm
                or BuffIconType.Polymorph
                or BuffIconType.HorrificBeast
                or BuffIconType.LichForm
                or BuffIconType.VampiricEmbrace
                or BuffIconType.ReaperForm
                or BuffIconType.WhiteTigerForm
                or BuffIconType.StoneForm
                or BuffIconType.MysticalPolymorphTotem
                => BuffArchetype.FormShift,

            // ----- Stealth / shadow -----
            BuffIconType.HidingAndOrStealth
                or BuffIconType.Invisibility
                or BuffIconType.Disguised
                or BuffIconType.Incognito
                or BuffIconType.Shadow
                or BuffIconType.WraithForm
                or BuffIconType.EtherealVoyage
                => BuffArchetype.Stealth,

            // ----- Fire / burning -----
            BuffIconType.ImmolatingWeapon
                or BuffIconType.ElementalFury
                or BuffIconType.ElementalFuryDebuff
                or BuffIconType.FistsOfFury
                or BuffIconType.Berserk
                or BuffIconType.DivineFury
                or BuffIconType.Rage
                or BuffIconType.Rampage
                or BuffIconType.BoneBreaker
                or BuffIconType.HeatOfBattleStatus
                => BuffArchetype.Fire,

            // ----- Lightning / storm / thunder -----
            BuffIconType.Thunderstorm
                or BuffIconType.LightningStrike
                or BuffIconType.Sparks
                or BuffIconType.EssenceOfWind
                or BuffIconType.PsychicAttack
                or BuffIconType.DeathRay
                or BuffIconType.DeathRayDebuff
                or BuffIconType.MysticWeapon
                or BuffIconType.EtherealBurst
                => BuffArchetype.Lightning,

            // ----- Wind / speed -----
            BuffIconType.Fly
                or BuffIconType.MomentumStrike
                or BuffIconType.TalonStrike
                or BuffIconType.Surge
                or BuffIconType.SwingSpeedDebuff
                or BuffIconType.Thrust
                or BuffIconType.ThrustDebuff
                or BuffIconType.Pierce
                or BuffIconType.ForceArrow
                or BuffIconType.CalledShot
                or BuffIconType.ArmorPierce
                or BuffIconType.SplinteringEffect
                => BuffArchetype.Wind,

            // ----- Holy / blessing / healing / virtues -----
            BuffIconType.Protection
                or BuffIconType.ArchProtection
                or BuffIconType.MagicReflection
                or BuffIconType.Healing
                or BuffIconType.GiftOfRenewal
                or BuffIconType.GiftOfLife
                or BuffIconType.AchievePerfection
                or BuffIconType.Perfection
                or BuffIconType.ConsecrateWeapon
                or BuffIconType.Humility
                or BuffIconType.Honored
                or BuffIconType.Confidence
                or BuffIconType.Evasion
                or BuffIconType.SavingThrow
                or BuffIconType.Conduit
                or BuffIconType.Enchant
                or BuffIconType.AttuneWeapon
                or BuffIconType.HonorableExecution
                or BuffIconType.PotionGloriousFortune
                or BuffIconType.CaddelliteInfused
                or BuffIconType.OrangePetals
                or BuffIconType.RoseOfTrinsic
                or BuffIconType.ManaPhase
                or BuffIconType.ActiveMeditation
                or BuffIconType.ArcaneEmpowerment
                or BuffIconType.SpellFocusingBuff
                or BuffIconType.RageFocusingBuff
                or BuffIconType.Warding
                or BuffIconType.PlayingTheOdds
                or BuffIconType.FocusedEye
                or BuffIconType.FishPie
                or BuffIconType.UraliTranceTonic
                or BuffIconType.BarakoDraftOfMight
                or BuffIconType.JukariBurnPoiltice
                or BuffIconType.SakkhraProphylaxis
                or BuffIconType.KurakAmbushersEssence
                or BuffIconType.BarrabHemolymphConcentrate
                => BuffArchetype.Holy,

            // ----- Curse / dark / unholy -----
            BuffIconType.Curse
                or BuffIconType.MassCurse
                or BuffIconType.BloodOathCurse
                or BuffIconType.BloodOathCaster
                or BuffIconType.CorpseSkin
                or BuffIconType.Mindrot
                or BuffIconType.PainSpike
                or BuffIconType.Strangle
                or BuffIconType.MortalStrike
                or BuffIconType.EvilOmen
                or BuffIconType.SpellPlague
                or BuffIconType.Bleed
                or BuffIconType.DeathStrike
                or BuffIconType.HonoredDebuff
                or BuffIconType.SpellFocusingDebuff
                or BuffIconType.RageFocusingDebuff
                or BuffIconType.GazeDespair
                or BuffIconType.DespairCaster
                or BuffIconType.DespairTarget
                or BuffIconType.TribulationCaster
                or BuffIconType.TribulationTarget
                or BuffIconType.TrueFear
                or BuffIconType.AuraOfNausea
                or BuffIconType.HowlOfCacophony
                or BuffIconType.RuneBeetleCorruption
                or BuffIconType.BloodwormAnemia
                or BuffIconType.RotwormBloodDisease
                or BuffIconType.MedusaStone
                or BuffIconType.HumilityDebuff
                or BuffIconType.Whispering
                => BuffArchetype.Curse,

            // ----- Poison / nature -----
            BuffIconType.Poison
                or BuffIconType.PoisonImmunity
                or BuffIconType.Veterinary
                or BuffIconType.Webbing
                or BuffIconType.Swarm
                or BuffIconType.SwarmImmune
                => BuffArchetype.Poison,

            // ----- Stat boost -----
            BuffIconType.Strength
                or BuffIconType.Agility
                or BuffIconType.Cunning
                or BuffIconType.Bless
                or BuffIconType.Inspire
                or BuffIconType.Invigorate
                or BuffIconType.Resilience
                or BuffIconType.Perseverance
                or BuffIconType.HeightenedSenses
                or BuffIconType.Tolerance
                or BuffIconType.Intuition
                or BuffIconType.CombatTraining
                or BuffIconType.EnchantedSummoning
                or BuffIconType.Toughness
                or BuffIconType.Potency
                or BuffIconType.Knockout
                => BuffArchetype.Stat,

            // ----- Defense / shield -----
            BuffIconType.ReactiveArmor
                or BuffIconType.ManaShield
                or BuffIconType.Block
                or BuffIconType.DefenseMastery
                or BuffIconType.DualWield
                or BuffIconType.AnticipateHit
                or BuffIconType.ShieldBash
                or BuffIconType.Bodyguard
                or BuffIconType.CounterAttack
                or BuffIconType.Feint
                or BuffIconType.FeintDebuff
                or BuffIconType.Disarm
                or BuffIconType.HitLowerAttack
                or BuffIconType.HitLowerDefense
                or BuffIconType.HiryuPhysicalResistance
                or BuffIconType.Onslaught
                or BuffIconType.EnemyOfOne
                or BuffIconType.EnemyOfOneDebuff
                or BuffIconType.CurseWeapon
                or BuffIconType.InjectedStrike
                or BuffIconType.InjectedStrikeDebuff
                or BuffIconType.Warcry
                => BuffArchetype.Defense,

            // ----- Debuff / disable / control -----
            BuffIconType.Clumsy
                or BuffIconType.FeebleMind
                or BuffIconType.Weaken
                or BuffIconType.Paralyze
                or BuffIconType.Sleep
                or BuffIconType.MassSleep
                or BuffIconType.FanDancerFanFire
                or BuffIconType.SkillUseDelay
                or BuffIconType.FactionStatLoss
                or BuffIconType.CriminalStatus
                or BuffIconType.Stagger
                or BuffIconType.DragonTurtleDebuff
                or BuffIconType.PlayingTheOddsDebuff
                or BuffIconType.NoRearm
                or BuffIconType.DismountPrevention
                or BuffIconType.Boarding
                or BuffIconType.NightSight
                or BuffIconType.CityTradeDeal
                => BuffArchetype.Debuff,

            // ----- Catch-all -----
            _ => BuffArchetype.Default,
        };
    }
}
