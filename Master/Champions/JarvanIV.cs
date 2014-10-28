﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using LX_Orbwalker;

namespace Master
{
    class JarvanIV : Program
    {
        private const String Version = "1.0.0";
        private Obj_AI_Base wallObj = null;
        private Vector3 flagPos = default(Vector3);

        public JarvanIV()
        {
            SkillQ = new Spell(SpellSlot.Q, 770);
            SkillW = new Spell(SpellSlot.W, 525);
            SkillE = new Spell(SpellSlot.E, 830);
            SkillR = new Spell(SpellSlot.R, 650);
            SkillQ.SetSkillshot(SkillQ.Instance.SData.SpellCastTime, SkillQ.Instance.SData.LineWidth, SkillQ.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotLine);

            Config.SubMenu("Orbwalker").SubMenu("lxOrbwalker_Modes").AddItem(new MenuItem(Name + "EQFlash", "Combo EQ Flash").SetValue(new KeyBind("X".ToCharArray()[0], KeyBindType.Press)));

            Config.AddSubMenu(new Menu("Combo", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "autowusage", "Use W If Hp Under").SetValue(new Slider(20, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "rusage", "Use R").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "ruseMode", "R Mode").SetValue(new StringList(new[] { "Finish", "# Enemy" })));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "rmulti", "Use R If Enemy Above").SetValue(new Slider(2, 1, 4)));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Harass", "hsettings"));
            Config.SubMenu("hsettings").AddItem(new MenuItem(Name + "useHarE", "Use E").SetValue(true));
            Config.SubMenu("hsettings").AddItem(new MenuItem(Name + "harMode", "Use EQ If Hp Above").SetValue(new Slider(20, 1)));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearE", "Use E").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearI", "Use Tiamat/Hydra Item").SetValue(true));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "lasthitQ", "Use Q To Last Hit").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "killstealQ", "Auto Q To Kill Steal").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "useInterEQ", "Use EQ To Interrupt").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "surviveW", "Try Use W To Survive").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "SkinID", "Skin Changer").SetValue(new Slider(5, 0, 6))).ValueChanged += SkinChanger;
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "packetCast", "Use Packet To Cast").SetValue(true));

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawQ", "Q Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawW", "W Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawE", "E Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawR", "R Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnCreate += OnCreate;
            Obj_AI_Base.OnDelete += OnDelete;
            Game.PrintChat("<font color = \"#33CCCC\">Master of {0}</font> <font color = \"#fff8e7\">Brian v{1}</font>", Name, Version);
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead) return;
            PacketCast = Config.Item(Name + "packetCast").GetValue<bool>();
            switch (LXOrbwalker.CurrentMode)
            {
                case LXOrbwalker.Mode.Combo:
                    NormalCombo();
                    break;
                case LXOrbwalker.Mode.Harass:
                    Harass();
                    break;
                case LXOrbwalker.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case LXOrbwalker.Mode.LaneFreeze:
                    LaneJungClear();
                    break;
                case LXOrbwalker.Mode.Lasthit:
                    if (Config.Item(Name + "lasthitQ").GetValue<bool>()) LastHit();
                    break;
                case LXOrbwalker.Mode.Flee:
                    Flee();
                    break;
            }
            if (Config.Item(Name + "EQFlash").GetValue<KeyBind>().Active)
            {
                ComboEQFlash();
                LXOrbwalker.CustomOrbwalkMode = true;
            }
            else LXOrbwalker.CustomOrbwalkMode = false;
            if (Config.Item(Name + "killstealQ").GetValue<bool>()) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item(Name + "DrawQ").GetValue<bool>() && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawW").GetValue<bool>() && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Config.Item(Name + "useInterEQ").GetValue<bool>() || !SkillQ.IsReady()) return;
            if (unit.IsValidTarget(SkillQ.Range) && SkillE.IsReady()) SkillE.Cast(unit.Position + Vector3.Normalize(unit.Position - Player.Position) * 100, PacketCast);
            if (flagPos != default(Vector3) && unit.IsValidTarget(180, true, flagPos)) SkillQ.Cast(flagPos, PacketCast);
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            if (sender.Name == "JarvanDemacianStandard_buf_green.troy") flagPos = sender.Position;
            if (sender.Name == "JarvanCataclysm_tar.troy") wallObj = (Obj_AI_Base)sender;
            if (sender is Obj_SpellMissile && sender.IsValid && Config.Item(Name + "surviveW").GetValue<bool>() && SkillW.IsReady())
            {
                var missle = (Obj_SpellMissile)sender;
                var caster = missle.SpellCaster;
                if (caster.IsEnemy)
                {
                    var ShieldBuff = new Int32[] { 50, 90, 130, 170, 210 }[SkillW.Level - 1] + new Int32[] { 20, 30, 40, 50, 60 }[SkillW.Level - 1] * Utility.CountEnemysInRange(300);
                    if (missle.SData.Name.Contains("BasicAttack"))
                    {
                        if (missle.Target.IsMe && Player.Health <= caster.GetAutoAttackDamage(Player, true) && Player.Health + ShieldBuff > caster.GetAutoAttackDamage(Player, true)) SkillW.Cast();
                    }
                    else if (missle.Target.IsMe || missle.EndPosition.Distance(Player.Position) <= 130)
                    {
                        if (missle.SData.Name == "summonerdot")
                        {
                            if (Player.Health <= (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite) && Player.Health + ShieldBuff > (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) SkillW.Cast();
                        }
                        else if (Player.Health <= caster.GetDamageSpell(Player, missle.SData.Name).CalculatedDamage && Player.Health + ShieldBuff > caster.GetDamageSpell(Player, missle.SData.Name).CalculatedDamage) SkillW.Cast();
                    }
                }
            }
        }

        private void OnDelete(GameObject sender, EventArgs args)
        {
            if (sender.Name == "JarvanDemacianStandard_buf_green.troy") flagPos = default(Vector3);
            if (sender.Name == "JarvanCataclysm_tar.troy") wallObj = null;
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item(Name + "eusage").GetValue<bool>() && SkillE.IsReady() && SkillQ.InRange(targetObj.Position))
            {
                if (LXOrbwalker.InAutoAttackRange(targetObj))
                {
                    SkillE.Cast(targetObj.Position, PacketCast);
                }
                else
                {
                    SkillE.Cast(targetObj.Position + Vector3.Normalize(targetObj.Position - Player.Position) * 100, PacketCast);
                }
            }
            if (Config.Item(Name + "eusage").GetValue<bool>() && flagPos != default(Vector3) && targetObj.IsValidTarget(180, true, flagPos))
            {
                if (Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.InRange(targetObj.Position) && SkillQ.IsReady()) SkillQ.Cast(flagPos, PacketCast);
            }
            else if (Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.InRange(targetObj.Position) && SkillQ.IsReady()) SkillQ.Cast(targetObj.Position, PacketCast);
            if (Config.Item(Name + "rusage").GetValue<bool>() && SkillR.IsReady() && wallObj == null)
            {
                switch (Config.Item(Name + "ruseMode").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        if (SkillR.InRange(targetObj.Position) && SkillR.IsKillable(targetObj)) SkillR.CastOnUnit(targetObj, PacketCast);
                        break;
                    case 1:
                        var UltiObj = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillR.Range) && Utility.CountEnemysInRange(i.Position, 325) >= Config.Item(Name + "rmulti").GetValue<Slider>().Value);
                        if (UltiObj != null) SkillR.CastOnUnit(UltiObj, PacketCast);
                        break;
                }
            }
            if (Config.Item(Name + "wusage").GetValue<bool>() && SkillW.IsReady() && SkillW.InRange(targetObj.Position) && Player.Health * 100 / Player.MaxHealth <= Config.Item(Name + "autowusage").GetValue<Slider>().Value) SkillW.Cast();
            if (Config.Item(Name + "iusage").GetValue<bool>()) UseItem(targetObj);
            if (Config.Item(Name + "ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null) return;
            if (Config.Item(Name + "useHarE").GetValue<bool>() && SkillE.IsReady() && SkillQ.InRange(targetObj.Position)) SkillE.Cast(targetObj.Position + Vector3.Normalize(targetObj.Position - Player.Position) * 100, PacketCast);
            if (Config.Item(Name + "useHarE").GetValue<bool>() && flagPos != default(Vector3) && targetObj.IsValidTarget(180, true, flagPos) && SkillQ.InRange(targetObj.Position) && SkillQ.IsReady())
            {
                if (Player.Health * 100 / Player.MaxHealth >= Config.Item(Name + "harMode").GetValue<Slider>().Value) SkillQ.Cast(flagPos, PacketCast);
            }
            else if (SkillQ.InRange(targetObj.Position) && SkillQ.IsReady()) SkillQ.Cast(targetObj.Position, PacketCast);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly);
            if (minionObj.Count() == 0) return;
            if (Config.Item(Name + "useClearE").GetValue<bool>() && SkillE.IsReady()) SkillE.Cast(SkillE.GetCircularFarmLocation(minionObj.ToList()).Position, PacketCast);
            if (Config.Item(Name + "useClearE").GetValue<bool>() && flagPos != default(Vector3) && minionObj.Count(i => i.IsValidTarget(180, true, flagPos)) >= 2)
            {
                if (Config.Item(Name + "useClearQ").GetValue<bool>() && SkillQ.IsReady()) SkillQ.Cast(flagPos, PacketCast);
            }
            else if (Config.Item(Name + "useClearQ").GetValue<bool>() && SkillQ.IsReady()) SkillQ.Cast(SkillQ.GetLineFarmLocation(minionObj.ToList()).Position, PacketCast);
            if (Config.Item(Name + "useClearI").GetValue<bool>() && minionObj.Count(i => i.IsValidTarget(350)) >= 2)
            {
                if (Items.CanUseItem(Tiamat)) Items.UseItem(Tiamat);
                if (Items.CanUseItem(Hydra)) Items.UseItem(Hydra);
            }
        }

        private void LastHit()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault(i => SkillQ.IsKillable(i));
            if (minionObj != null && SkillQ.IsReady()) SkillQ.Cast(minionObj.Position, PacketCast);
        }

        private void Flee()
        {
            if (!SkillQ.IsReady()) return;
            if (SkillE.IsReady()) SkillE.Cast(Game.CursorPos, PacketCast);
            if (SkillQ.IsReady() && flagPos != default(Vector3)) SkillQ.Cast(flagPos, PacketCast);
        }

        private void ComboEQFlash()
        {
            LXOrbwalker.Orbwalk(Game.CursorPos, targetObj);
            if (targetObj == null || !FlashReady() || Player.Mana < SkillQ.Instance.ManaCost || Player.Distance(targetObj) > SkillE.Range + 1000) return;
            if (SkillE.IsReady()) SkillE.Cast(Game.CursorPos, PacketCast);
            if (SkillQ.IsReady() && flagPos != default(Vector3) && SkillQ.InRange(flagPos)) SkillQ.Cast(flagPos, PacketCast);
            if (targetObj.IsValidTarget(1000, true, Player.ServerPosition)) CastFlash(targetObj.Position);
        }

        private void KillSteal()
        {
            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillQ.Range) && SkillQ.IsKillable(i) && i != targetObj);
            if (target != null && SkillQ.IsReady()) SkillQ.Cast(target.Position, PacketCast);
        }

        private void UseItem(Obj_AI_Hero target)
        {
            if (Items.CanUseItem(Tiamat) && Utility.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && (Utility.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(target) < target.Health && Utility.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Rand) && Utility.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
        }
    }
}