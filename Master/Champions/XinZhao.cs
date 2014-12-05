﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace MasterPlugin
{
    class XinZhao : Master.Program
    {
        public XinZhao()
        {
            SkillQ = new Spell(SpellSlot.Q, 375);
            SkillW = new Spell(SpellSlot.W, 20);
            SkillE = new Spell(SpellSlot.E, 650);
            SkillR = new Spell(SpellSlot.R, 500);
            SkillQ.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            SkillE.SetTargetted(-0.5f, 20);
            SkillR.SetSkillshot(-0.5f, 0, 347.8f, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu(Name + " Plugin", Name + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R If Killable");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    var SmiteMob = new Menu("Smite Mob If Killable", "SmiteMob");
                    {
                        ItemBool(SmiteMob, "Baron", "Baron Nashor");
                        ItemBool(SmiteMob, "Dragon", "Dragon");
                        ItemBool(SmiteMob, "Red", "Red Brambleback");
                        ItemBool(SmiteMob, "Blue", "Blue Sentinel");
                        ItemBool(SmiteMob, "Krug", "Ancient Krug");
                        ItemBool(SmiteMob, "Gromp", "Gromp");
                        ItemBool(SmiteMob, "Raptor", "Crimson Raptor");
                        ItemBool(SmiteMob, "Wolf", "Greater Murk Wolf");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy)) ItemBool(UltiMenu, Obj.ChampionName, "Use R On " + Obj.ChampionName);
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "RInterrupt", "Use R To Interrupt");
                    ItemBool(MiscMenu, "EKillSteal", "Use E To Kill Steal");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 5).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ItemBool(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
            if (ItemBool("Misc", "EKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "E") && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "RInterrupt") || Player.IsDead) return;
            if (!IsValid(unit, SkillR.Range) && SkillR.IsReady() && SkillE.IsReady() && Player.Mana >= SkillE.Instance.ManaCost + SkillR.Instance.ManaCost)
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, SkillE.Range) && !(i is Obj_AI_Turret) && i != unit && i.Distance3D(unit) <= SkillR.Range - 30)) SkillE.CastOnUnit(Obj, PacketCast());
            }
            if (IsValid(unit, SkillR.Range) && SkillR.IsReady() && !unit.HasBuff("XinZhaoIntimidate")) SkillR.Cast(PacketCast());
        }

        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (!unit.IsMe) return;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && ItemBool(Orbwalk.CurrentMode.ToString(), "Q") && IsValid(target, Orbwalk.GetAutoAttackRange() + 50) && SkillQ.IsReady() && target.HasBuff("XinZhaoIntimidate")) SkillQ.Cast(PacketCast());
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "R") && ItemBool("Ultimate", targetObj.ChampionName) && Mode == "Combo" && SkillR.IsReady() && SkillR.InRange(targetObj.Position))
            {
                if (CanKill(targetObj, SkillR))
                {
                    SkillR.Cast(PacketCast());
                }
                else if (SkillR.GetHealthPrediction(targetObj) - SkillR.GetDamage(targetObj) + 5 <= SkillE.GetDamage(targetObj) + Player.GetAutoAttackDamage(targetObj, true) + ((ItemBool(Mode, "Q") && SkillQ.IsReady()) ? SkillQ.GetDamage(targetObj) * 3 : 0) && ItemBool(Mode, "E") && SkillE.IsReady() && Player.Mana >= SkillR.Instance.ManaCost + SkillE.Instance.ManaCost + ((ItemBool(Mode, "Q") && SkillQ.IsReady()) ? SkillQ.Instance.ManaCost : 0)) SkillR.Cast(PacketCast());
            }
            if (ItemBool(Mode, "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && (CanKill(targetObj, SkillE) || Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange() + 50 || (Mode == "Combo" && Player.HealthPercentage() < targetObj.HealthPercentage()))) SkillE.CastOnUnit(targetObj, PacketCast());
            if (ItemBool(Mode, "W") && SkillW.IsReady() && Orbwalk.InAutoAttackRange(targetObj)) SkillW.Cast(PacketCast());
            if (ItemBool(Mode, "Item") && Mode == "Combo") UseItem(targetObj);
            if (ItemBool(Mode, "Ignite") && Mode == "Combo") CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, SkillE.Range)).OrderBy(i => i.Health))
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && SkillE.IsReady() && (Player.Distance3D(Obj) > Orbwalk.GetAutoAttackRange() + 50 || CanKill(Obj, SkillE) || Obj.MaxHealth >= 1200)) SkillE.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "W") && SkillW.IsReady() && Orbwalk.InAutoAttackRange(Obj)) SkillW.Cast(PacketCast());
                if (ItemBool("Clear", "Q") && SkillQ.IsReady() && Player.Distance3D(Obj) <= Orbwalk.GetAutoAttackRange() + 50) SkillQ.Cast(PacketCast());
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void KillSteal()
        {
            if (!SkillE.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, SkillE.Range) && CanKill(i, SkillE) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) SkillE.CastOnUnit(Obj, PacketCast());
        }

        private void UseItem(Obj_AI_Base Target, bool Farm = false)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450 && !Farm) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450 && !Farm) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Tiamat) && Farm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && Farm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !Farm) Items.UseItem(Randuin);
            if (Items.CanUseItem(Youmuu) && Player.CountEnemysInRange(350) >= 1 && !Farm) Items.UseItem(Youmuu);
        }
    }
}