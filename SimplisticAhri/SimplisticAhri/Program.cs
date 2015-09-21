﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using Version = System.Version;

namespace SimplisticAhri
{
    internal class Program
    {
        public static Dictionary<SpellSlot, Spell.SpellBase> Spells = new Dictionary<SpellSlot, Spell.SpellBase>
        {
            {
                SpellSlot.Q, new Spell.Skillshot(SpellSlot.Q, 1000, SkillShotType.Linear, 250, 1600, 50)
            },
            {
                SpellSlot.W, new Spell.Active(SpellSlot.W, 700)
            },
            {
                SpellSlot.E, new Spell.Skillshot(SpellSlot.E, 1000, SkillShotType.Linear, 250, 1550, 60)
            },
            {
                SpellSlot.R, new Spell.Skillshot(SpellSlot.R, 900, SkillShotType.Linear, 250)
            }
        };

        public static Dictionary<SpellSlot, int> Mana = new Dictionary<SpellSlot, int>
        {
            {
                SpellSlot.Q, new[]
                {
                    65, 70, 75, 80, 85
                }[Spells[SpellSlot.Q].IsLearned ? Spells[SpellSlot.Q].Level - 1 : 0]
            },
            {
                SpellSlot.W, 50
            },
            {
                SpellSlot.E, 85
            },
            {
                SpellSlot.R, 100
            }
        };

        public static Version Version;

        private static readonly Spell.Skillshot SpellE = new Spell.Skillshot(SpellSlot.E, 1000, SkillShotType.Linear,
            250, 1550, 60);

        public static Menu menu, ComboMenu, HarassMenu, FarmMenu, KillStealMenu, JungleMenu, FleeMenu, GapMenu;
        public static CheckBox SmartMode;

        private static Dictionary<string, object> _Q = new Dictionary<string, object>() { { "MinSpeed", 400 }, { "MaxSpeed", 2500 }, { "Acceleration", -3200 }, { "Speed1", 1400 }, { "Delay1", 250 }, { "Range1", 880 }, { "Delay2", 0 }, { "Range2", int.MaxValue }, { "IsReturning", false }, { "Target", null }, { "Object", null }, { "LastObjectVector", null }, { "LastObjectVectorTime", null }, { "CatchPosition", null } };
        private static Dictionary<string, object> _E = new Dictionary<string, object>() { { "LastCastTime", 0f }, { "Object", null }, };
        private static Dictionary<string, object> _R = new Dictionary<string, object>() { { "EndTime", 0f }, };
        private static Vector3 mousePos
        {
            get { return Game.CursorPos; }
        }

        public static AIHeroClient _Player
        {
            get { return ObjectManager.Player; }
        }

        private static void Main(string[] args)
        {
            Loading.OnLoadingComplete += Loading_OnLoadingComplete;
            Version = Assembly.GetExecutingAssembly().GetName().Version;
        }

        private static void Loading_OnLoadingComplete(EventArgs args)
        {
            if (_Player.Hero != Champion.Ahri)
            {
                Chat.Print("Champion not supported!");
                return;
            }
            Chat.Print("<b>Simplistic Ahri</b> - Loaded!");

            Bootstrap.Init(null);

            menu = MainMenu.AddMenu("Simplistic Ahri1", "simplisticahri");
            menu.AddGroupLabel("Simplistic Ahri");
            menu.AddLabel("This project is being updated daily.");
            menu.AddLabel("Expect Bugs and bad Prediction!");
            menu.AddSeparator();
            SmartMode = menu.Add("smartMode", new CheckBox("Smart Mana Management", true));
            menu.AddLabel("Harass Smart Mana Mode");

            ComboMenu = menu.AddSubMenu("Combo", "ComboAhri");
            ComboMenu.Add("useQCombo", new CheckBox("Use Q"));
            ComboMenu.Add("useWCombo", new CheckBox("Use W"));
            ComboMenu.Add("useECombo", new CheckBox("Use E"));
            ComboMenu.Add("SmartUlt", new CheckBox("Smart R "));
            ComboMenu.Add("UltInit", new CheckBox("Don't Initiate with R", false));
            ComboMenu.Add("useCharm", new CheckBox("Smart Charm Combo"));

            KillStealMenu = menu.AddSubMenu("Killsteal", "ksAhri");
            KillStealMenu.Add("useKS", new CheckBox("Killsteal on?"));
            KillStealMenu.Add("useQKS", new CheckBox("Use Q for KS"));
            KillStealMenu.Add("useWKS", new CheckBox("Use W for KS"));
            KillStealMenu.Add("useEKS", new CheckBox("Use E for KS"));
            KillStealMenu.Add("useRKS", new CheckBox("Use R for KS"));

            HarassMenu = menu.AddSubMenu("Harass", "HarassAhri");
            HarassMenu.Add("useQHarass", new CheckBox("Use Q"));
            HarassMenu.Add("useWHarass", new CheckBox("Use W"));
            HarassMenu.Add("useEHarass", new CheckBox("Use E"));

            FarmMenu = menu.AddSubMenu("Farm", "FarmAhri");
            FarmMenu.AddLabel("Coming Soon");
            FarmMenu.Add("qlh", new CheckBox("Use Q LastHit"));
            FarmMenu.Add("Mana", new Slider("Min. Mana Percent:", 20, 0, 100));

            JungleMenu = menu.AddSubMenu("JungleClear", "JungleClear");
            JungleMenu.Add("Q", new CheckBox("Use Q", true));
            JungleMenu.Add("W", new CheckBox("Use W", true));
            JungleMenu.Add("E", new CheckBox("Use E", true));
            JungleMenu.Add("Mana", new Slider("Min. Mana Percent:", 20, 0, 100));

            FleeMenu = menu.AddSubMenu("Flee", "Flee");
            FleeMenu.Add("R", new CheckBox("Use R to mousePos", true));

            GapMenu = menu.AddSubMenu("Auto E ", "autoe");
            GapMenu.Add("GapE", new CheckBox("Use E on Gapclosers", true));
            GapMenu.Add("IntE", new CheckBox("Use E on Interruptable Spells", true));


            SpellE.AllowedCollisionCount = 0;
            Game.OnTick += Game_OnTick;
            Gapcloser.OnGapCloser += Gapcloser_OnGapCloser;
            Interrupter.OnInterruptableSpell += Interrupter_OnInterruptableSpell;
        }

        private static void Game_OnTick(EventArgs args)
        {
            Orbwalker.ForcedTarget = null;
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo)) Combo();
            else if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass)) Harass();
            else if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LastHit)) LastHit();
            else if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear))
            {
                WaveClear();
                JungleClear();
            }
            else if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Flee)) Flee();
            KillSteal();
        }

        static void Gapcloser_OnGapCloser(AIHeroClient sender, Gapcloser.GapCloserEventArgs gapcloser)
        {
            if (!GapMenu["GapE"].Cast<CheckBox>().CurrentValue) return;
            if (ObjectManager.Player.Distance(gapcloser.Sender, true) < Spells[SpellSlot.E].Range * Spells[SpellSlot.E].Range)
            {
                Spells[SpellSlot.E].Cast(gapcloser.Sender);
            }
        }

        static void Interrupter_OnInterruptableSpell(Obj_AI_Base sender, InterruptableSpellEventArgs args)
        {
            if (!GapMenu["IntE"].Cast<CheckBox>().CurrentValue) return;

            if (ObjectManager.Player.Distance(sender, true) < Spells[SpellSlot.E].Range * Spells[SpellSlot.E].Range)
            {
                Spells[SpellSlot.E].Cast(sender);
            }
        }

        public static void WaveClear()
        {
            var minions = ObjectManager.Get<Obj_AI_Base>()
                .Where(
                    a =>
                        a.IsEnemy && a.Distance(_Player) <= _Player.AttackRange &&
                        a.Health <= _Player.GetAutoAttackDamage(a) * 1.1);
            var minions2 = ObjectManager.Get<Obj_AI_Base>()
                .Where(
                    a =>
                        a.IsEnemy && a.Distance(_Player) <= _Player.AttackRange &&
                        a.Health <= _Player.GetAutoAttackDamage(a) * 1.1);
            var minion = minions.OrderByDescending(a => minions2.Count(b => b.Distance(a) <= 200)).FirstOrDefault();
            Orbwalker.ForcedTarget = minion;
        }

        public static void KillSteal()
        {
            if (KillStealMenu["useKS"].Cast<CheckBox>().CurrentValue)
            {
                var kstarget = TargetSelector.GetTarget(2500, DamageType.Magical);

                if (kstarget.IsValidTarget(Spells[SpellSlot.E].Range) && kstarget.HealthPercent <= 40)
                {
                    if (KillStealMenu["useQKS"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.Q].IsReady() &&
                        kstarget.Distance(_Player) < Spells[SpellSlot.Q].Range &&
                        Damage(kstarget, SpellSlot.Q) >= kstarget.Health)
                    {
                        var predQ = Prediction.Position.PredictLinearMissile(kstarget, Spells[SpellSlot.Q].Range, 50,
                            250,
                            1600, 999);
                        Spells[SpellSlot.Q].Cast(predQ.CastPosition);
                    }

                    if (KillStealMenu["useEKS"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.E].IsReady() &&
                        kstarget.Distance(_Player) < Spells[SpellSlot.E].Range &&
                        Damage(kstarget, SpellSlot.E) >= kstarget.Health)
                    {
                        var e = SpellE.GetPrediction(kstarget);
                        if (e.HitChance >= HitChance.High)
                        {
                            var predE = Prediction.Position.PredictLinearMissile(kstarget, Spells[SpellSlot.E].Range, 60,
                                250,
                                1550, 0);
                            Spells[SpellSlot.E].Cast(predE.CastPosition);
                        }
                    }

                    if (KillStealMenu["useRKS"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.R].IsReady() &&
                        kstarget.Distance(_Player) < 400 && Damage(kstarget, SpellSlot.R) >= kstarget.Health)
                    {
                        Spells[SpellSlot.R].Cast(kstarget);
                    }

                    if (KillStealMenu["useWKS"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.W].IsReady() &&
                        kstarget.Distance(_Player) < Spells[SpellSlot.W].Range &&
                        Damage(kstarget, SpellSlot.W) >= kstarget.Health)
                    {
                        Spells[SpellSlot.W].Cast();
                    }
                }
            }
        }

        public static void Harass()
        {
            var target = TargetSelector.GetTarget(1550, DamageType.Physical);

            if (target == null) return;

            if (Orbwalker.IsAutoAttacking) return;

            if (HarassMenu["useQHarass"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.Q].IsReady())
            {
                if (target.Distance(_Player) <= Spells[SpellSlot.Q].Range ||
                    (_Player.ManaPercent > 40 && SmartMode.CurrentValue))
                {
                    var predQ = Prediction.Position.PredictLinearMissile(target, Spells[SpellSlot.Q].Range, 50, 250,
                        1600, 999);
                    Spells[SpellSlot.Q].Cast(predQ.CastPosition);
                    return;
                }
            }

            if (HarassMenu["useWHarass"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.W].IsReady())
            {
                if (target.Distance(_Player) <= Spells[SpellSlot.W].Range ||
                    (_Player.ManaPercent > 40 && SmartMode.CurrentValue))
                {
                    Spells[SpellSlot.W].Cast();
                    return;
                }
            }

            if (HarassMenu["useEHarass"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.E].IsReady())
            {
                if (target.Distance(_Player) <= Spells[SpellSlot.E].Range ||
                    (_Player.ManaPercent > 40 && SmartMode.CurrentValue))
                {
                    var e = SpellE.GetPrediction(target);
                    if (e.HitChance >= HitChance.High)
                    {
                        var predE = Prediction.Position.PredictLinearMissile(target, Spells[SpellSlot.E].Range, 60,
                            250,
                            1550, 0);
                        Spells[SpellSlot.E].Cast(predE.CastPosition);
                    }
                }
            }
        }

        public static void Combo()
        {
            var target = TargetSelector.GetTarget(1550, DamageType.Magical);
            var charmed = HeroManager.Enemies.Find(h => h.HasBuffOfType(BuffType.Charm));
            var cc = HeroManager.Enemies.Find(h => h.HasBuffOfType(BuffType.Fear));

            if (target == null) return;

            if (Orbwalker.IsAutoAttacking) return;

            if (ComboMenu["useCharm"].Cast<CheckBox>().CurrentValue && charmed != null)
            {
                target = charmed;
            } else if (ComboMenu["useCharm"].Cast<CheckBox>().CurrentValue && charmed == null && cc != null)
            {

                target = cc;

            }
            else
            {
                target = TargetSelector.GetTarget(1550, DamageType.Magical);
            }


            HandleRCombo(target);

            if (ComboMenu["useECombo"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.E].IsReady())
            {
                var e = SpellE.GetPrediction(target);
                if (e.HitChance >= HitChance.High)
                {
                    var predE = Prediction.Position.PredictLinearMissile(target, Spells[SpellSlot.E].Range, 60,
                        250,
                        1550, 0);
                    Spells[SpellSlot.E].Cast(predE.CastPosition);
                }
            }

            if (ComboMenu["useQCombo"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.Q].IsReady())
            {
                var predQ = Prediction.Position.PredictLinearMissile(target, Spells[SpellSlot.Q].Range, 50, 250, 1600,
                    999);
                Spells[SpellSlot.Q].Cast(predQ.CastPosition);
                return;
            }

            if (ComboMenu["useWCombo"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.W].IsReady())
            {
                Spells[SpellSlot.W].Cast();
            }
        }


        private static void JungleClear()
        {
            if (_Player.ManaPercent >= JungleMenu["Mana"].Cast<Slider>().CurrentValue)
            {
                foreach (Obj_AI_Base minion in EntityManager.GetJungleMonsters(_Player.Position.To2D(), 1000f))
                {
                    if (minion.IsValidTarget() && _Player.ManaPercent >= JungleMenu["Mana"].Cast<Slider>().CurrentValue)
                    {
                        if (JungleMenu["E"].Cast<CheckBox>().CurrentValue)
                        {
                            Spells[SpellSlot.E].Cast(minion);
                        }
                        if (JungleMenu["Q"].Cast<CheckBox>().CurrentValue)
                        {
                            Spells[SpellSlot.Q].Cast(minion);
                        }
                        if (JungleMenu["W"].Cast<CheckBox>().CurrentValue)
                        {
                            Spells[SpellSlot.W].Cast(minion);
                        }
                    }
                }
            }
        }


        private static void Flee()
        {
            if (FleeMenu["R"].Cast<CheckBox>().CurrentValue && Spells[SpellSlot.R].IsReady())
            {
                Spells[SpellSlot.R].Cast(mousePos);
            }
        }

        private static void HandleRCombo(AIHeroClient target)
        {
            if (Spells[SpellSlot.R].IsReady() && target.IsValidTarget())
            {
                
                if (ComboMenu["SmartUlt"].Cast<CheckBox>().CurrentValue)
                {
                    if ((float)_R["EndTime"] > ((float)0))
                    { 
                        if (_Q["Object"] != null)
                        {
                            if ((bool)_Q["IsReturning"] && Extensions.Distance(_Player, (GameObject)_Q["Object"]) < Extensions.Distance(_Player, (Obj_AI_Base)_Q["Target"]))
                            {
                                Spells[SpellSlot.R].Cast(mousePos);
                            }
                            else
                            {
                                return;
                            }
                        }
                        if (!Spells[SpellSlot.Q].IsReady() && (float)_R["EndTime"] - Game.Time <= _Player.Spellbook.GetSpell(Spells[SpellSlot.R].Slot).Cooldown)
                        {
                            Spells[SpellSlot.R].Cast(mousePos);
                        }
                    }
                    if (GetComboDamage(target) >= target.Health && Extensions.Distance(mousePos, target) < Extensions.Distance(_Player, target))
                    {

                            if (Extensions.Distance(_Player, target) > 400)
                            {
                                Spells[SpellSlot.R].Cast(mousePos);
                            }
                    }
                }
                else
                {
                    Spells[SpellSlot.R].Cast(mousePos);
                }
            }
        }


        public static float GetComboDamage(AIHeroClient enemy)
        {
            float totalDamage = 0;
            totalDamage += Spells[SpellSlot.Q].IsReady() ? (_Player.GetSpellDamage(enemy, SpellSlot.Q)) : 0;
            totalDamage += Spells[SpellSlot.W].IsReady() ? (_Player.GetSpellDamage(enemy, SpellSlot.W)) : 0;
            totalDamage += Spells[SpellSlot.E].IsReady() ? (_Player.GetSpellDamage(enemy, SpellSlot.E)) : 0;
            totalDamage += (Spells[SpellSlot.R].IsReady() || (RStacks() != 0))
                ? (_Player.GetSpellDamage(enemy, SpellSlot.R))
                : 0;
            return totalDamage;
        }

        public static int RStacks()
        {
            var rBuff = ObjectManager.Player.Buffs.Find(buff => buff.Name == "AhriTumble");
            return rBuff != null ? rBuff.Count : 0;
        }


        private static void LastHit()
        {
            if (ObjectManager.Player.ManaPercent < FarmMenu["Mana"].Cast<Slider>().CurrentValue)
            {
                return;
            }
                var minions = ObjectManager.Get<Obj_AI_Base>()
                    .Where(
                        a =>
                            a.IsEnemy && a.Distance(_Player) <= Spells[SpellSlot.Q].Range &&
                            a.Health <= _Player.GetSpellDamage(a, SpellSlot.Q) * 1.1);

                var minions2 = ObjectManager.Get<Obj_AI_Base>()
                    .Where(
                        a =>
                            a.IsEnemy && a.Distance(_Player) <= Spells[SpellSlot.Q].Range &&
                            a.Health <= _Player.GetSpellDamage(a, SpellSlot.Q) * 1.1);

                var minion = minions.OrderByDescending(a => minions2.Count(b => b.Distance(a) <= 200)).FirstOrDefault();
                Orbwalker.ForcedTarget = minion;
        }

        private static float Damage(Obj_AI_Base target, SpellSlot slot)
        {
            if (target.IsValidTarget())
            {
                if (slot == SpellSlot.Q)
                {
                    return
                        _Player.CalculateDamageOnUnit(target, DamageType.Magical,
                            (float)25 * Spells[SpellSlot.Q].Level + 15 + 0.35f * _Player.FlatMagicDamageMod) +
                        _Player.CalculateDamageOnUnit(target, DamageType.True,
                            (float)25 * Spells[SpellSlot.Q].Level + 15 + 0.35f * _Player.FlatMagicDamageMod);
                }
                if (slot == SpellSlot.W)
                {
                    return 1.6f *
                           _Player.CalculateDamageOnUnit(target, DamageType.Magical,
                               (float)25 * Spells[SpellSlot.W].Level + 15 + 0.4f * _Player.FlatMagicDamageMod);
                }
                if (slot == SpellSlot.E)
                {
                    return _Player.CalculateDamageOnUnit(target, DamageType.Magical,
                        (float)35 * Spells[SpellSlot.E].Level + 25 + 0.5f * _Player.FlatMagicDamageMod);
                }
                if (slot == SpellSlot.R)
                {
                    return 3 *
                           _Player.CalculateDamageOnUnit(target, DamageType.Magical,
                               (float)40 * Spells[SpellSlot.R].Level + 30 + 0.3f * _Player.FlatMagicDamageMod);
                }
            }
            return _Player.GetSpellDamage(target, slot);
        }

        static void OnCreateObj(EloBuddy.GameObject sender, EventArgs args)
        {
            var missile = (MissileClient)sender;
            if (missile == null || !missile.IsValid || missile.SpellCaster == null || !missile.SpellCaster.IsValid)
            {
                return;
            }
            var unit = (Obj_AI_Base)missile.SpellCaster;
            if (missile.SpellCaster.IsMe)
            {
                var name = missile.SData.Name.ToLower();
                if (name.Contains("ahriorbmissile"))
                {
                    _Q["Object"] = sender;
                    _Q["IsReturning"] = false;
                }
                else if (name.Contains("ahriorbreturn"))
                {
                    _Q["Object"] = sender;
                    _Q["IsReturning"] = true;
                }
                else if (name.Contains("ahriseducemissile"))
                {
                    _E["Object"] = sender;
                }
            }
        }

        static void OnDeleteObj(GameObject sender, EventArgs args)
        {
            var missile = (MissileClient)sender;
            if (missile == null || !missile.IsValid || missile.SpellCaster == null || !missile.SpellCaster.IsValid)
            {
                return;
            }
            var unit = (Obj_AI_Base)missile.SpellCaster;
            if (missile.SpellCaster.IsMe)
            {
                var name = missile.SData.Name.ToLower();
                if (name.Contains("ahriorbreturn"))
                {
                    _Q["Object"] = null;
                    _Q["IsReturning"] = false;
                    _Q["Target"] = null;
                    _Q["LastObjectVector"] = null;
                }
                else if (name.Contains("ahriseducemissile"))
                {
                    _E["Object"] = null;
                }
            }
        }
    }
}