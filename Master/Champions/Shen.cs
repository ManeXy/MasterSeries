﻿using System;
using System.Linq;
using System.Collections.Generic;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace MasterPlugin
{
    class Shen : Master.Program
    {
        private bool PingCasted = false;

        public Shen()
        {
            SkillQ = new Spell(SpellSlot.Q, 475);
            SkillW = new Spell(SpellSlot.W, 20);
            SkillE = new Spell(SpellSlot.E, 600);
            SkillR = new Spell(SpellSlot.R, 25000);
            SkillQ.SetTargetted(-0.5f, 1500);
            SkillE.SetSkillshot(-0.5f, 50, 0, false, SkillshotType.SkillshotLine);

            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem(Name + "_OW_FlashTaunt", "Flash Taunt").SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu(Name + " Plugin", Name + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemSlider(ComboMenu, "WUnder", "-> If Hp Under", 20);
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemSlider(HarassMenu, "EAbove", "-> If Hp Above", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    ItemBool(UltiMenu, "Alert", "Alert Ally Low Hp");
                    ItemSlider(UltiMenu, "HpUnder", "-> If Hp Under", 30);
                    ItemBool(UltiMenu, "Ping", "-> Ping Fallback");
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "EAntiGap", "Use E To Anti Gap Closer");
                    ItemBool(MiscMenu, "EInterrupt", "Use E To Interrupt");
                    ItemBool(MiscMenu, "EUnderTower", "Use E If Enemy Under Tower");
                    ItemBool(MiscMenu, "WSurvive", "Try Use W To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 6, 0, 6).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
        }

        private void OnGameUpdate(EventArgs args)
        {
            //Passive: Shen Passive Aura
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && SkillE.IsReady()) SkillE.Cast(Game.CursorPos, PacketCast());
            if (ItemActive("FlashTaunt"))
            {
                FlashTaunt();
            }
            else Orbwalk.CustomMode = false;
            if (ItemBool("Ultimate", "Alert")) UltimateAlert();
            if (ItemBool("Misc", "EUnderTower")) AutoEUnderTower();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "EAntiGap") || Player.IsDead) return;
            if (IsValid(gapcloser.Sender, Orbwalk.GetAutoAttackRange() + 100) && SkillE.IsReady()) SkillE.Cast(Player.Position.To2D().Extend(gapcloser.Sender.Position.To2D(), gapcloser.Sender.Distance3D(Player) + 200), PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EInterrupt") || Player.IsDead) return;
            if (IsValid(unit, SkillE.Range) && SkillE.IsReady()) SkillE.Cast(Player.Position.To2D().Extend(unit.Position.To2D(), unit.Distance3D(Player) + 200), PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsEnemy && ItemBool("Misc", "WSurvive") && SkillW.IsReady())
            {
                if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)) || (args.SData.Name == "summonerdot" && Player.Health <= (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite))))
                {
                    SkillW.Cast(PacketCast());
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i)) SkillW.Cast(PacketCast());
                        }
                    }
                }
            }
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "Item") && Mode == "Combo")
            {
                if (Items.CanUseItem(Deathfire) && Player.Distance3D(targetObj) <= 750) Items.UseItem(Deathfire, targetObj);
                if (Items.CanUseItem(Blackfire) && Player.Distance3D(targetObj) <= 750) Items.UseItem(Blackfire, targetObj);
                if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
            }
            if (ItemBool(Mode, "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && (Mode == "Combo" || Player.HealthPercentage() >= ItemSlider(Mode, "EAbove"))) SkillE.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
            if (ItemBool(Mode, "Q") && SkillQ.IsReady() && SkillQ.InRange(targetObj.Position)) SkillQ.CastOnUnit(targetObj, PacketCast());
            if (ItemBool(Mode, "W") && Mode == "Combo" && SkillW.IsReady() && Orbwalk.InAutoAttackRange(targetObj) && Player.HealthPercentage() <= ItemSlider(Mode, "WUnder")) SkillW.Cast(PacketCast());
            if (ItemBool(Mode, "Ignite") && Mode == "Combo") CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, SkillQ.Range)).OrderBy(i => i.Health))
            {
                if (ItemBool("Clear", "Q") && SkillQ.IsReady()) SkillQ.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "W") && SkillW.IsReady() && Orbwalk.InAutoAttackRange(Obj)) SkillW.Cast(PacketCast());
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !SkillQ.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, SkillQ.Range) && CanKill(i, SkillQ)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) SkillQ.CastOnUnit(Obj, PacketCast());
        }

        private void FlashTaunt()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null || !FlashReady() || !SkillE.IsReady()) return;
            if (Player.Distance3D(targetObj) <= SkillE.Range + 390)
            {
                SkillE.Cast(targetObj.Position, PacketCast());
                Utility.DelayAction.Add(300, () => CastFlash(targetObj.Position));
            }
        }

        private void UltimateAlert()
        {
            if (!SkillR.IsReady() || PingCasted) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, float.MaxValue, false) && i.CountEnemysInRange(800) >= 1 && i.HealthPercentage() <= ItemSlider("Ultimate", "HpUnder")))
            {
                Game.PrintChat("<font color = \'{0}'>-></font> <font color = \'{1}'>{2}</font>: <font color = \'{3}'>In Dangerous</font>", Master.HtmlColor.BlueViolet, Master.HtmlColor.Gold, Obj.ChampionName, Master.HtmlColor.Cyan);
                if (ItemBool("Ultimate", "Ping"))
                {
                    for (var i = 0; i < 3; i++) Packet.S2C.Ping.Encoded(new Packet.S2C.Ping.Struct(Obj.Position.X, Obj.Position.Y, Obj.NetworkId, 0, Packet.PingType.Fallback)).Process();
                    PingCasted = true;
                    Utility.DelayAction.Add(5000, () => PingCasted = false);
                }
            }
        }

        private void AutoEUnderTower()
        {
            if (Player.UnderTurret() || !SkillE.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, SkillE.Range)))
            {
                var TowerObj = ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => IsValid(i, 950, false));
                if (TowerObj != null && Obj.Distance3D(TowerObj) <= 950) SkillE.Cast(Player.Position.To2D().Extend(Obj.Position.To2D(), Obj.Distance3D(Player) + 200), PacketCast());
            }
        }
    }
}